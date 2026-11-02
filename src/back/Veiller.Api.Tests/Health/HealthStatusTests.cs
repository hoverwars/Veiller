using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using AuthRole = Veiller.Api.Auth.Role;

namespace Veiller.Api.Tests.Health;

/// <summary><c>GET /api/health/me/status</c> and the integration of health data with the profile export and account deletion.</summary>
public sealed class HealthStatusTests : HealthApiTestBase
{
    public HealthStatusTests() => SeedUserAsync(Elder, AuthRole.Elder, inFirebase: true).GetAwaiter().GetResult();

    private static void AssertSameInstant(DateTimeOffset expected, DateTimeOffset? actual) =>
        Assert.True(actual is not null && (actual.Value - expected).Duration() < TimeSpan.FromSeconds(1), $"expected {expected:O}, got {actual:O}");

    // ---- GET /status ----

    [Fact]
    public async Task Status_without_any_data_is_200_with_every_field_null()
    {
        var status = await ReadStatusAsync(Elder);

        Assert.Equal(JsonValueKind.Null, status.GetProperty("lastUploadAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, status.GetProperty("watchLastSeenAt").ValueKind);
        foreach (var field in new[] { "heartRate", "steps", "sleep" })
            Assert.Equal(JsonValueKind.Null, status.GetProperty("lastSample").GetProperty(field).ValueKind);
    }

    [Fact]
    public async Task An_empty_sync_still_counts_as_an_upload()
    {
        var before = DateTimeOffset.UtcNow;
        await SyncOkAsync(Elder, Body());

        var status = await ReadStatusAsync(Elder);

        Assert.InRange(ReadInstant(status, "lastUploadAt")!.Value, before.AddSeconds(-1), DateTimeOffset.UtcNow.AddSeconds(1));
        Assert.Equal(JsonValueKind.Null, status.GetProperty("lastSample").GetProperty("heartRate").ValueKind);
    }

    [Fact]
    public async Task Upload_time_is_the_server_time_returned_by_the_sync()
    {
        var response = await SyncOkAsync(Elder, Body());

        var status = await ReadStatusAsync(Elder);

        AssertSameInstant(ReadInstant(response, "serverTime")!.Value, ReadInstant(status, "lastUploadAt"));
    }

    [Fact]
    public async Task Last_sample_reports_the_latest_stored_time_of_each_type()
    {
        await SyncOkAsync(Elder, Body(
            heartRate: [Record("hr-1", T0, Sample(T0.AddMinutes(1), 70), Sample(T0.AddMinutes(9), 71)), Record("hr-2", T0.AddHours(-3), Sample(T0.AddHours(-3).AddMinutes(4), 65))],
            steps: [Hour(T0, 100), Hour(T0.AddHours(1), 50), Hour(T0.AddHours(2), 0)],
            sleep: [Session("s-1", T0.AddHours(-30), T0.AddHours(-23)), Session("s-2", T0.AddHours(-10), T0.AddHours(-3))]));

        var status = await ReadStatusAsync(Elder);

        AssertSameInstant(T0.AddMinutes(9), ReadInstant(status, "lastSample", "heartRate"));
        // The latest bucket WITH steps: the trailing zero bucket does not count, and the instant is the bucket's end.
        AssertSameInstant(T0.AddHours(2), ReadInstant(status, "lastSample", "steps"));
        AssertSameInstant(T0.AddHours(-3), ReadInstant(status, "lastSample", "sleep"));
    }

    [Fact]
    public async Task Last_steps_is_null_while_every_bucket_is_zero()
    {
        await SyncOkAsync(Elder, Body(steps: [Hour(T0, 0), Hour(T0.AddHours(1), 0)]));

        Assert.Equal(JsonValueKind.Null, (await ReadStatusAsync(Elder)).GetProperty("lastSample").GetProperty("steps").ValueKind);
    }

    [Fact]
    public async Task Status_only_reports_the_caller_data()
    {
        await SeedUserAsync("elder-2");
        await SyncOkAsync("elder-2", Body(heartRate: [Record("hr-1", T0, Sample(T0.AddMinutes(1), 70))], watchLastSeenAt: T0));

        var status = await ReadStatusAsync(Elder);

        Assert.Equal(JsonValueKind.Null, status.GetProperty("lastUploadAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, status.GetProperty("watchLastSeenAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, status.GetProperty("lastSample").GetProperty("heartRate").ValueKind);
    }

    // ---- watchLastSeenAt ----

    [Fact]
    public async Task Watch_last_seen_only_moves_forward()
    {
        var recent = DateTimeOffset.UtcNow.AddMinutes(-10);
        await SyncOkAsync(Elder, Body(watchLastSeenAt: recent));

        // A late retry of an older request cannot move it back; a request without the field keeps it.
        await SyncOkAsync(Elder, Body(watchLastSeenAt: recent.AddHours(-5)));
        await SyncOkAsync(Elder, Body());
        AssertSameInstant(recent, ReadInstant(await ReadStatusAsync(Elder), "watchLastSeenAt"));

        await SyncOkAsync(Elder, Body(watchLastSeenAt: recent.AddMinutes(5)));
        AssertSameInstant(recent.AddMinutes(5), ReadInstant(await ReadStatusAsync(Elder), "watchLastSeenAt"));
    }

    [Fact]
    public async Task Repeating_a_request_does_not_change_watch_last_seen()
    {
        var seen = DateTimeOffset.UtcNow.AddMinutes(-3);
        var body = Body(watchLastSeenAt: seen);

        await SyncOkAsync(Elder, body);
        await SyncOkAsync(Elder, body);

        AssertSameInstant(seen, ReadInstant(await ReadStatusAsync(Elder), "watchLastSeenAt"));
    }

    // ---- Export and deletion (profile) ----

    [Fact]
    public async Task Profile_export_of_an_elder_includes_the_health_data()
    {
        await SyncOkAsync(Elder, Body(
            heartRate: [Record("hr-1", T0, Sample(T0.AddMinutes(2), 71), Sample(T0.AddMinutes(1), 70))],
            steps: [Hour(T0, 120)],
            sleep: [Session("s-1", T0, T0.AddHours(7), Stage(T0, T0.AddHours(7), "DEEP"))],
            watchLastSeenAt: T0));

        var response = await SendRawAsync(HttpMethod.Get, "/api/profile/export", Elder, null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var health = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("health");
        var record = health.GetProperty("heartRateRecords")[0];
        Assert.Equal("hr-1", record.GetProperty("hcRecordId").GetString());
        Assert.Equal([70, 71], record.GetProperty("samples").EnumerateArray().Select(s => s.GetProperty("bpm").GetInt32()));
        Assert.Equal(120, health.GetProperty("stepsHourly")[0].GetProperty("count").GetInt32());
        var session = health.GetProperty("sleepSessions")[0];
        Assert.Equal("-05:00", session.GetProperty("zoneOffset").GetString());
        Assert.Equal("DEEP", session.GetProperty("stages")[0].GetProperty("stage").GetString());
        AssertSameInstant(T0, ReadInstant(health, "status", "watchLastSeenAt"));
    }

    [Fact]
    public async Task Profile_export_of_a_caregiver_has_no_health_section()
    {
        await SeedUserAsync("cg", AuthRole.Caregiver, inFirebase: true);

        var response = await SendRawAsync(HttpMethod.Get, "/api/profile/export", "cg", null);

        Assert.Equal(JsonValueKind.Null, (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("health").ValueKind);
    }

    [Fact]
    public async Task Deleting_the_account_deletes_every_health_row()
    {
        await SeedUserAsync("elder-2");
        await SyncOkAsync(Elder, Body(
            heartRate: [Record("hr-1", T0, Sample(T0.AddMinutes(1), 70))], steps: [Hour(T0, 5)],
            sleep: [Session("s-1", T0, T0.AddHours(7), Stage(T0, T0.AddHours(7), "LIGHT"))], watchLastSeenAt: T0));
        await SyncOkAsync("elder-2", Body(heartRate: [Record("hr-1", T0, Sample(T0.AddMinutes(1), 90))]));

        var response = await SendRawAsync(HttpMethod.Delete, "/api/profile", Elder, null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, await StoredRowCountAsync(Elder));
        using var _ = Factory.CreateDbScope(out var db);
        Assert.False(await db.HealthSyncStatuses.AnyAsync(s => s.ElderId == Elder, Ct));
        // Another elder's data is not collateral damage.
        Assert.Single(await StoredSamplesAsync("elder-2"));
    }
}
