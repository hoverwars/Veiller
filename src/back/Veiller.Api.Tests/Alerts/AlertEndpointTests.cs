using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Veiller.Api.Data.Entities;

namespace Veiller.Api.Tests.Alerts;

public sealed class AlertEndpointTests : AlertApiTestBase
{
    private const string Admin = "admin-1";
    private const string Plain = "caregiver-2";
    private readonly Guid _circle;

    public AlertEndpointTests()
    {
        _circle = SeedAdminCircleAsync(Elder, Admin, Plain).GetAwaiter().GetResult();
        SeedUserAsync("stranger", AuthRoleCaregiver).GetAwaiter().GetResult();
    }

    private const Veiller.Api.Auth.Role AuthRoleCaregiver = Veiller.Api.Auth.Role.Caregiver;

    private async Task<Guid> SeedAlertAsync(AlertType type = AlertType.HeartRateHigh, AlertStatus status = AlertStatus.New, int minutesAgo = 0, Guid? circle = null)
    {
        using var _ = Factory.CreateDbScope(out var db);
        var alert = new Alert { CircleId = circle ?? _circle, Type = type, Status = status, OpenedAt = DateTimeOffset.UtcNow.AddMinutes(-minutesAgo), Value = 130 };
        db.Alerts.Add(alert);
        await db.SaveChangesAsync(Ct);
        return alert.Id;
    }

    private Task<HttpResponseMessage> PostAsync(string url, string? uid) => SendJsonAsync(HttpMethod.Post, url, uid, null);

    // ---- List ----

    [Fact]
    public async Task Caregivers_list_the_alerts_newest_first()
    {
        await SeedAlertAsync(AlertType.SleepLow, minutesAgo: 30);
        await SeedAlertAsync(AlertType.HeartRateHigh, minutesAgo: 5);

        var alerts = await AlertsOfAsync(_circle, Plain);

        Assert.Equal(["HeartRateHigh", "SleepLow"], alerts.EnumerateArray().Select(a => a.GetProperty("type").GetString()));
        Assert.Equal("New", alerts[0].GetProperty("status").GetString());
        Assert.Equal(130, alerts[0].GetProperty("value").GetInt32());
    }

    [Fact]
    public async Task Status_filter_and_limit_apply()
    {
        await SeedAlertAsync(status: AlertStatus.New, minutesAgo: 1);
        await SeedAlertAsync(status: AlertStatus.Resolved, minutesAgo: 2);
        await SeedAlertAsync(status: AlertStatus.Resolved, minutesAgo: 3);

        Assert.Equal(2, (await AlertsOfAsync(_circle, Plain, "?status=resolved")).GetArrayLength());
        Assert.Equal(1, (await AlertsOfAsync(_circle, Plain, "?status=RESOLVED&limit=1")).GetArrayLength());
    }

    [Theory]
    [InlineData("?status=bogus")]
    [InlineData("?status=5")]
    [InlineData("?limit=0")]
    [InlineData("?limit=201")]
    public async Task Invalid_query_is_400(string query)
    {
        var response = await GetAsync($"/api/circles/{_circle}/alerts{query}", Plain);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_requires_authentication()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync($"/api/circles/{_circle}/alerts", null)).StatusCode);
    }

    [Theory]
    [InlineData("stranger")]
    [InlineData(Elder)]
    public async Task Non_members_and_the_data_owner_get_404(string uid)
    {
        await SeedAlertAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync($"/api/circles/{_circle}/alerts", uid)).StatusCode);
    }

    [Fact]
    public async Task Unknown_circle_is_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync($"/api/circles/{Guid.NewGuid()}/alerts", Plain)).StatusCode);
    }

    // ---- Seen / resolve ----

    [Fact]
    public async Task Seen_moves_a_new_alert_and_is_idempotent()
    {
        var id = await SeedAlertAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync($"/api/circles/{_circle}/alerts/{id}/seen", Plain)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync($"/api/circles/{_circle}/alerts/{id}/seen", Plain)).StatusCode);

        Assert.Equal(AlertStatus.Seen, (await StoredAlertsAsync(_circle)).Single().Status);
    }

    [Fact]
    public async Task Seen_does_not_reopen_a_resolved_alert()
    {
        var id = await SeedAlertAsync(status: AlertStatus.Resolved);

        await PostAsync($"/api/circles/{_circle}/alerts/{id}/seen", Plain);

        Assert.Equal(AlertStatus.Resolved, (await StoredAlertsAsync(_circle)).Single().Status);
    }

    [Fact]
    public async Task Resolve_closes_the_alert_with_a_date_and_is_idempotent()
    {
        var id = await SeedAlertAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync($"/api/circles/{_circle}/alerts/{id}/resolve", Admin)).StatusCode);
        var first = (await StoredAlertsAsync(_circle)).Single();
        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync($"/api/circles/{_circle}/alerts/{id}/resolve", Admin)).StatusCode);

        Assert.Equal(AlertStatus.Resolved, first.Status);
        Assert.NotNull(first.ResolvedAt);
        Assert.Equal(first.ResolvedAt, (await StoredAlertsAsync(_circle)).Single().ResolvedAt);
    }

    [Theory]
    [InlineData("seen")]
    [InlineData("resolve")]
    public async Task Changing_an_alert_requires_a_caregiver_of_its_circle(string action)
    {
        var id = await SeedAlertAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await PostAsync($"/api/circles/{_circle}/alerts/{id}/{action}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PostAsync($"/api/circles/{_circle}/alerts/{id}/{action}", "stranger")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PostAsync($"/api/circles/{_circle}/alerts/{id}/{action}", Elder)).StatusCode);
        Assert.Equal(AlertStatus.New, (await StoredAlertsAsync(_circle)).Single().Status);
    }

    [Theory]
    [InlineData("seen")]
    [InlineData("resolve")]
    public async Task An_unknown_alert_is_404(string action)
    {
        Assert.Equal(HttpStatusCode.NotFound, (await PostAsync($"/api/circles/{_circle}/alerts/{Guid.NewGuid()}/{action}", Plain)).StatusCode);
    }

    [Fact]
    public async Task An_alert_of_another_circle_cannot_be_changed_through_this_one()
    {
        var other = await SeedCircleAsync("elder-9", "caregiver-9");
        var foreign = await SeedAlertAsync(circle: other);

        Assert.Equal(HttpStatusCode.NotFound, (await PostAsync($"/api/circles/{_circle}/alerts/{foreign}/resolve", Plain)).StatusCode);
        Assert.Equal(AlertStatus.New, (await StoredAlertsAsync(other)).Single().Status);
    }

    // ---- Rules ----

    private async Task<JsonElement> RulesAsync(string uid)
    {
        var response = await GetAsync($"/api/circles/{_circle}/alert-rules", uid);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("rules");
    }

    private Task<HttpResponseMessage> PutRulesAsync(string? uid, object? body) => SendJsonAsync(HttpMethod.Put, $"/api/circles/{_circle}/alert-rules", uid, body);

    private static object Rule(object? type, object? enabled, object? threshold) => new { type, enabled, threshold };

    [Fact]
    public async Task Rules_default_to_the_product_values_for_every_member()
    {
        foreach (var uid in new[] { Plain, Admin, Elder })
        {
            var rules = await RulesAsync(uid);
            Assert.Equal(4, rules.GetArrayLength());
            var high = rules.EnumerateArray().Single(r => r.GetProperty("type").GetString() == "HeartRateHigh");
            Assert.Equal(120, high.GetProperty("threshold").GetInt32());
            Assert.Equal("bpm", high.GetProperty("unit").GetString());
            Assert.True(high.GetProperty("enabled").GetBoolean());
        }
    }

    [Fact]
    public async Task Admin_saves_rules_and_the_others_keep_their_default()
    {
        var response = await PutRulesAsync(Admin, new { rules = new[] { Rule("HeartRateHigh", true, 110), Rule("SleepLow", false, 300) } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rules = await RulesAsync(Plain);
        JsonElement Find(string type) => rules.EnumerateArray().Single(r => r.GetProperty("type").GetString() == type);
        Assert.Equal(110, Find("HeartRateHigh").GetProperty("threshold").GetInt32());
        Assert.False(Find("SleepLow").GetProperty("enabled").GetBoolean());
        Assert.Equal(40, Find("HeartRateLow").GetProperty("threshold").GetInt32());
    }

    [Fact]
    public async Task Saving_twice_updates_in_place()
    {
        await PutRulesAsync(Elder, new { rules = new[] { Rule("NoSync", true, 120) } });
        await PutRulesAsync(Elder, new { rules = new[] { Rule("NoSync", true, 240) } });

        using var _ = Factory.CreateDbScope(out var db);
        Assert.Equal(240, (await db.AlertRules.SingleAsync(Ct)).Threshold);
    }

    [Fact]
    public async Task A_plain_caregiver_cannot_change_the_rules()
    {
        var response = await PutRulesAsync(Plain, new { rules = new[] { Rule("HeartRateHigh", true, 110) } });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(120, (await RulesAsync(Plain)).EnumerateArray().First().GetProperty("threshold").GetInt32());
    }

    [Fact]
    public async Task Rules_require_authentication_and_membership()
    {
        var body = new { rules = new[] { Rule("HeartRateHigh", true, 110) } };

        Assert.Equal(HttpStatusCode.Unauthorized, (await PutRulesAsync(null, body)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PutRulesAsync("stranger", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync($"/api/circles/{_circle}/alert-rules", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync($"/api/circles/{_circle}/alert-rules", "stranger")).StatusCode);
    }

    public static TheoryData<string, object?> InvalidRuleBodies => new()
    {
        { "null body", null },
        { "no rules", new { } },
        { "empty rules", new { rules = Array.Empty<object>() } },
        { "unknown type", new { rules = new[] { Rule("Bogus", true, 100) } } },
        { "numeric type", new { rules = new[] { Rule(99, true, 100) } } },
        { "missing type", new { rules = new[] { Rule(null, true, 100) } } },
        { "missing enabled", new { rules = new[] { Rule("HeartRateHigh", null, 100) } } },
        { "missing threshold", new { rules = new[] { Rule("HeartRateHigh", true, null) } } },
        { "duplicate type", new { rules = new[] { Rule("HeartRateHigh", true, 100), Rule("HeartRateHigh", true, 110) } } },
        { "high too low", new { rules = new[] { Rule("HeartRateHigh", true, 59) } } },
        { "high too high", new { rules = new[] { Rule("HeartRateHigh", true, 251) } } },
        { "low too low", new { rules = new[] { Rule("HeartRateLow", true, 19) } } },
        { "sleep too short", new { rules = new[] { Rule("SleepLow", true, 59) } } },
        { "sleep too long", new { rules = new[] { Rule("SleepLow", true, 721) } } },
        { "silence too short", new { rules = new[] { Rule("NoSync", true, 29) } } },
        { "negative", new { rules = new[] { Rule("NoSync", true, -5) } } },
        { "high not above low", new { rules = new[] { Rule("HeartRateHigh", true, 90), Rule("HeartRateLow", true, 95) } } },
    };

    [Theory]
    [MemberData(nameof(InvalidRuleBodies))]
    public async Task Invalid_rules_are_400_and_nothing_is_stored(string _, object? body)
    {
        var response = await PutRulesAsync(Admin, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = Factory.CreateDbScope(out var db);
        Assert.Empty(await db.AlertRules.ToListAsync(Ct));
    }

    [Fact]
    public async Task Moving_one_bound_across_the_stored_other_is_400()
    {
        // Default low is 40: a ceiling of 60 is fine, but not after the floor was raised to 70.
        await PutRulesAsync(Admin, new { rules = new[] { Rule("HeartRateLow", true, 70) } });

        var response = await PutRulesAsync(Admin, new { rules = new[] { Rule("HeartRateHigh", true, 65) } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Malformed_json_is_400()
    {
        var response = await SendRawAsync(HttpMethod.Put, $"/api/circles/{_circle}/alert-rules", Admin, "{not json");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

public sealed class DeviceTokenEndpointTests : AlertApiTestBase
{
    public DeviceTokenEndpointTests()
    {
        SeedUserAsync(Caregiver, Veiller.Api.Auth.Role.Caregiver).GetAwaiter().GetResult();
        SeedUserAsync("caregiver-2", Veiller.Api.Auth.Role.Caregiver).GetAwaiter().GetResult();
    }

    private Task<HttpResponseMessage> RegisterAsync(string? uid, object? body) => SendJsonAsync(HttpMethod.Put, "/api/devices/token", uid, body);

    private Task<HttpResponseMessage> UnregisterAsync(string? uid, object? body) => SendJsonAsync(HttpMethod.Delete, "/api/devices/token", uid, body);

    private async Task<List<DeviceToken>> TokensAsync()
    {
        using var _ = Factory.CreateDbScope(out var db);
        return await db.DeviceTokens.AsNoTracking().ToListAsync(Ct);
    }

    [Fact]
    public async Task A_token_is_registered_and_registering_again_is_harmless()
    {
        Assert.Equal(HttpStatusCode.NoContent, (await RegisterAsync(Caregiver, new { token = "abc", platform = "Android" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await RegisterAsync(Caregiver, new { token = "abc", platform = "android" })).StatusCode);

        var stored = Assert.Single(await TokensAsync());
        Assert.Equal((Caregiver, "android"), (stored.UserId, stored.Platform));
    }

    [Fact]
    public async Task A_token_moves_to_the_user_who_registers_it_last()
    {
        await RegisterAsync(Caregiver, new { token = "abc", platform = "android" });
        await RegisterAsync("caregiver-2", new { token = "abc", platform = "android" });

        Assert.Equal("caregiver-2", Assert.Single(await TokensAsync()).UserId);
    }

    public static TheoryData<string, object?> InvalidRegistrations => new()
    {
        { "null body", null },
        { "no token", new { platform = "android" } },
        { "blank token", new { token = "   ", platform = "android" } },
        { "token too long", new { token = new string('x', 513), platform = "android" } },
        { "no platform", new { token = "abc" } },
        { "unknown platform", new { token = "abc", platform = "symbian" } },
    };

    [Theory]
    [MemberData(nameof(InvalidRegistrations))]
    public async Task Invalid_registrations_are_400(string _, object? body)
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await RegisterAsync(Caregiver, body)).StatusCode);
        Assert.Empty(await TokensAsync());
    }

    [Fact]
    public async Task Token_endpoints_require_authentication()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await RegisterAsync(null, new { token = "abc", platform = "android" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await UnregisterAsync(null, new { token = "abc" })).StatusCode);
    }

    [Fact]
    public async Task A_user_removes_only_their_own_token()
    {
        await RegisterAsync(Caregiver, new { token = "mine", platform = "android" });
        await RegisterAsync("caregiver-2", new { token = "theirs", platform = "android" });

        Assert.Equal(HttpStatusCode.NoContent, (await UnregisterAsync(Caregiver, new { token = "theirs" })).StatusCode);
        Assert.Equal(2, (await TokensAsync()).Count);

        Assert.Equal(HttpStatusCode.NoContent, (await UnregisterAsync(Caregiver, new { token = "mine" })).StatusCode);
        Assert.Equal("theirs", Assert.Single(await TokensAsync()).Token);
        Assert.Equal(HttpStatusCode.NoContent, (await UnregisterAsync(Caregiver, new { token = "mine" })).StatusCode);
    }

    [Fact]
    public async Task Unregistering_without_a_token_is_400()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await UnregisterAsync(Caregiver, new { })).StatusCode);
    }
}
