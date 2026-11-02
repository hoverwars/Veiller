using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Veiller.Api.Alerts;
using Veiller.Api.Consents;
using Veiller.Api.Data.Entities;

namespace Veiller.Api.Tests.Alerts;

/// <summary>Alerts are opened and closed by the data a sync stores, then pushed to the caregivers of the circle.</summary>
public sealed class AlertEvaluationTests : AlertApiTestBase
{
    public AlertEvaluationTests() => SeedCircleAsync(Elder, Caregiver, "caregiver-2").GetAwaiter().GetResult();

    private async Task<Guid> CircleIdAsync()
    {
        using var _ = Factory.CreateDbScope(out var db);
        return await db.Circles.Where(c => c.OwnerUserId == Elder).Select(c => c.Id).SingleAsync(Ct);
    }

    [Fact]
    public async Task High_heart_rate_opens_an_alert()
    {
        await SyncRecentBpmAsync(Elder, "hr-1", 130);

        var alerts = await StoredAlertsAsync(await CircleIdAsync());

        var alert = Assert.Single(alerts);
        Assert.Equal(AlertType.HeartRateHigh, alert.Type);
        Assert.Equal(AlertStatus.New, alert.Status);
        Assert.Equal(130, alert.Value);
    }

    [Fact]
    public async Task Low_heart_rate_opens_an_alert()
    {
        await SyncRecentBpmAsync(Elder, "hr-1", 35);

        Assert.Equal(AlertType.HeartRateLow, Assert.Single(await StoredAlertsAsync(await CircleIdAsync())).Type);
    }

    [Fact]
    public async Task Normal_heart_rate_opens_nothing()
    {
        await SyncRecentBpmAsync(Elder, "hr-1", 72);

        Assert.Empty(await StoredAlertsAsync(await CircleIdAsync()));
    }

    [Fact]
    public async Task An_old_reading_is_not_judged()
    {
        await SyncHeartRateAsync(Elder, "hr-old", DateTimeOffset.UtcNow.AddHours(-5), 150);

        Assert.Empty(await StoredAlertsAsync(await CircleIdAsync()));
    }

    [Fact]
    public async Task The_same_episode_opens_a_single_alert()
    {
        await SyncRecentBpmAsync(Elder, "hr-1", 130);
        await SyncRecentBpmAsync(Elder, "hr-2", 135);
        await SyncRecentBpmAsync(Elder, "hr-1", 130);

        Assert.Single(await StoredAlertsAsync(await CircleIdAsync()));
    }

    [Fact]
    public async Task An_alert_resolves_itself_when_the_heart_rate_is_back_to_normal()
    {
        await SyncRecentBpmAsync(Elder, "hr-1", 130);
        await SyncHeartRateAsync(Elder, "hr-2", DateTimeOffset.UtcNow.AddMinutes(-1), 70);

        var alert = Assert.Single(await StoredAlertsAsync(await CircleIdAsync()));
        Assert.Equal(AlertStatus.Resolved, alert.Status);
        Assert.NotNull(alert.ResolvedAt);
        Assert.False(alert.Active);
    }

    [Fact]
    public async Task A_new_episode_after_a_resolved_one_opens_a_new_alert()
    {
        await SyncRecentBpmAsync(Elder, "hr-1", 130);
        await SyncHeartRateAsync(Elder, "hr-2", DateTimeOffset.UtcNow.AddMinutes(-1), 70);
        await SyncHeartRateAsync(Elder, "hr-3", DateTimeOffset.UtcNow, 140);

        Assert.Equal(2, (await StoredAlertsAsync(await CircleIdAsync())).Count);
    }

    [Fact]
    public async Task A_resolved_alert_does_not_come_back_while_the_condition_lasts()
    {
        var circle = await CircleIdAsync();
        await SyncRecentBpmAsync(Elder, "hr-1", 130);
        var alertId = (await StoredAlertsAsync(circle)).Single().Id;
        Assert.Equal(HttpStatusCode.NoContent, (await SendJsonAsync(HttpMethod.Post, $"/api/circles/{circle}/alerts/{alertId}/resolve", Caregiver, null)).StatusCode);

        await SyncRecentBpmAsync(Elder, "hr-2", 132);

        Assert.Single(await StoredAlertsAsync(circle));
    }

    [Fact]
    public async Task Without_a_reading_a_heart_rate_alert_is_left_open()
    {
        var circle = await CircleIdAsync();
        await SyncRecentBpmAsync(Elder, "hr-1", 130);

        await SyncOkAsync(Elder, Body(steps: [Hour(T0, 10)]));

        Assert.Equal(AlertStatus.New, Assert.Single(await StoredAlertsAsync(circle)).Status);
    }

    [Fact]
    public async Task Revoked_heart_rate_consent_disables_heart_rate_alerts()
    {
        await RevokeConsentAsync(Elder, HealthDataType.HeartRate);
        await SyncRecentBpmAsync(Elder, "hr-1", 150);

        Assert.Empty(await StoredAlertsAsync(await CircleIdAsync()));
    }

    [Fact]
    public async Task A_custom_threshold_of_the_circle_applies()
    {
        var circle = await CircleIdAsync();
        await SetRuleAsync(circle, AlertType.HeartRateHigh, enabled: true, threshold: 100);

        await SyncRecentBpmAsync(Elder, "hr-1", 110);

        Assert.Equal(AlertType.HeartRateHigh, Assert.Single(await StoredAlertsAsync(circle)).Type);
    }

    [Fact]
    public async Task A_disabled_rule_opens_nothing()
    {
        var circle = await CircleIdAsync();
        await SetRuleAsync(circle, AlertType.HeartRateHigh, enabled: false, threshold: 120);

        await SyncRecentBpmAsync(Elder, "hr-1", 150);

        Assert.Empty(await StoredAlertsAsync(circle));
    }

    [Fact]
    public async Task Short_sleep_opens_a_sleep_alert()
    {
        var end = DateTimeOffset.UtcNow.AddHours(-1);
        await SyncOkAsync(Elder, Body(sleep: [Session("night", end.AddHours(-3), end)]));

        var alert = Assert.Single(await StoredAlertsAsync(await CircleIdAsync()));
        Assert.Equal(AlertType.SleepLow, alert.Type);
        Assert.Equal(180, alert.Value);
    }

    [Fact]
    public async Task Enough_sleep_opens_nothing()
    {
        var end = DateTimeOffset.UtcNow.AddHours(-1);
        await SyncOkAsync(Elder, Body(sleep: [Session("night", end.AddHours(-7), end)]));

        Assert.Empty(await StoredAlertsAsync(await CircleIdAsync()));
    }

    // ---- Push ----

    [Fact]
    public async Task Caregivers_are_pushed_a_data_only_message_without_health_values()
    {
        await AddDeviceTokenAsync(Caregiver, "token-c1");
        await AddDeviceTokenAsync("caregiver-2", "token-c2");
        await AddDeviceTokenAsync(Elder, "token-elder");
        await SeedUserAsync("stranger", Veiller.Api.Auth.Role.Caregiver);
        await AddDeviceTokenAsync("stranger", "token-stranger");

        await SyncRecentBpmAsync(Elder, "hr-1", 131);

        var (tokens, message) = Assert.Single(Factory.Push.Sent);
        Assert.Equal(["token-c1", "token-c2"], tokens.Order());
        Assert.Equal("HeartRateHigh", message.Data["type"]);
        Assert.DoesNotContain(message.Data.Values, v => v.Contains("131"));
    }

    [Fact]
    public async Task A_continuing_episode_is_pushed_once()
    {
        await AddDeviceTokenAsync(Caregiver, "token-c1");

        await SyncRecentBpmAsync(Elder, "hr-1", 130);
        await SyncRecentBpmAsync(Elder, "hr-2", 131);

        Assert.Single(Factory.Push.Sent);
    }

    [Fact]
    public async Task Tokens_refused_by_the_provider_are_deleted()
    {
        await AddDeviceTokenAsync(Caregiver, "dead");
        await AddDeviceTokenAsync(Caregiver, "alive");
        Factory.Push.InvalidTokens.Add("dead");

        await SyncRecentBpmAsync(Elder, "hr-1", 130);

        using var _ = Factory.CreateDbScope(out var db);
        Assert.Equal(["alive"], await db.DeviceTokens.Select(t => t.Token).ToListAsync(Ct));
    }

    [Fact]
    public async Task A_push_failure_does_not_fail_the_sync()
    {
        await AddDeviceTokenAsync(Caregiver, "token-c1");
        Factory.Push.Failure = new InvalidOperationException("fcm down");

        await SyncRecentBpmAsync(Elder, "hr-1", 130);

        Assert.Single(await StoredAlertsAsync(await CircleIdAsync()));
        Assert.Single(await StoredSamplesAsync(Elder));
    }

    [Fact]
    public async Task An_elder_without_a_circle_can_still_sync()
    {
        await SeedUserAsync(OtherElder);

        await SyncRecentBpmAsync(OtherElder, "hr-1", 150);

        Assert.Single(await StoredSamplesAsync(OtherElder));
    }

    // ---- Silence of the watch (periodic job) ----

    private async Task<int> RunWorkerAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<AlertOptions>>().Value;
        return await AlertWorker.RunOnceAsync(scope.ServiceProvider, options, Ct);
    }

    private async Task SeedStatusAsync(DateTime lastUpload, DateTime? watchSeen = null)
    {
        using var _ = Factory.CreateDbScope(out var db);
        db.HealthSyncStatuses.Add(new HealthSyncStatus { ElderId = Elder, LastUploadAt = lastUpload, WatchLastSeenAt = watchSeen });
        await db.SaveChangesAsync(Ct);
    }

    [Fact]
    public async Task A_silent_watch_opens_a_no_sync_alert_and_pushes_it()
    {
        await AddDeviceTokenAsync(Caregiver, "token-c1");
        await SeedStatusAsync(DateTime.UtcNow.AddHours(-10));

        Assert.Equal(1, await RunWorkerAsync());

        var alert = Assert.Single(await StoredAlertsAsync(await CircleIdAsync()));
        Assert.Equal(AlertType.NoSync, alert.Type);
        Assert.InRange(alert.Value!.Value, 599, 601);
        Assert.Single(Factory.Push.Sent);
        Assert.Equal(0, await RunWorkerAsync());
    }

    [Fact]
    public async Task The_watch_signal_wins_over_the_upload_time()
    {
        await SeedStatusAsync(DateTime.UtcNow.AddHours(-10), watchSeen: DateTime.UtcNow.AddMinutes(-20));

        Assert.Equal(0, await RunWorkerAsync());
    }

    [Fact]
    public async Task A_no_sync_alert_resolves_when_the_watch_is_back()
    {
        await SeedStatusAsync(DateTime.UtcNow.AddHours(-10));
        await RunWorkerAsync();

        await SyncOkAsync(Elder, Body(watchLastSeenAt: DateTimeOffset.UtcNow.AddMinutes(-1)));

        Assert.Equal(AlertStatus.Resolved, Assert.Single(await StoredAlertsAsync(await CircleIdAsync())).Status);
    }

    [Fact]
    public async Task An_elder_who_never_synced_is_not_reported_silent()
    {
        Assert.Equal(0, await RunWorkerAsync());
    }
}
