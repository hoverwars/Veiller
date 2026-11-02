using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Veiller.Api.Consents;
using Veiller.Api.Data.Entities;
using Veiller.Api.Tests.Infrastructure;
using AuthRole = Veiller.Api.Auth.Role;

namespace Veiller.Api.Tests.Consents;

public sealed class ConsentEndpointTests : IDisposable
{
    private static readonly string[] AllTypes =
        ["HeartRate", "Location", "Sleep", "ScreenTime", "BloodPressure", "Spo2", "Ecg"];

    private static readonly string[] RequiredTypes = ["HeartRate", "Location", "Sleep", "ScreenTime"];

    private readonly VeillerApiFactory _factory = new();
    private readonly HttpClient _client;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ConsentEndpointTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // ---- Helpers ----

    private async Task SeedUserAsync(string uid, AuthRole role)
    {
        using var _ = _factory.CreateDbScope(out var db);
        if (await db.Users.AnyAsync(u => u.Id == uid, Ct)) return;
        db.Users.Add(new User { Id = uid, Email = $"{uid}@example.com", FullName = $"Name {uid}", Role = role, CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(Ct);
    }

    private async Task<Guid> SeedCircleAsync(string ownerId, params (string UserId, MemberRole Role)[] others)
    {
        await SeedUserAsync(ownerId, AuthRole.Elder);
        foreach (var (uid, _) in others) await SeedUserAsync(uid, AuthRole.Caregiver);

        using var scope = _factory.CreateDbScope(out var db);
        var now = DateTimeOffset.UtcNow;
        var circle = new Circle { OwnerUserId = ownerId, CreatedAt = now };
        circle.Members.Add(new CircleMember { CircleId = circle.Id, UserId = ownerId, Role = MemberRole.DataOwner, JoinedAt = now });
        foreach (var (uid, role) in others)
            circle.Members.Add(new CircleMember { CircleId = circle.Id, UserId = uid, Role = role, JoinedAt = now });
        db.Circles.Add(circle);
        await db.SaveChangesAsync(Ct);
        return circle.Id;
    }

    private Task<HttpResponseMessage> GetAsync(string url, string? uid) => SendAsync(HttpMethod.Get, url, uid, null);

    private Task<HttpResponseMessage> PutAsync(string? uid, string? json) => SendAsync(HttpMethod.Put, "/api/consents", uid, json);

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string? uid, string? json)
    {
        var request = new HttpRequestMessage(method, url);
        if (uid is not null) request.Headers.Add(TestAuthHandler.UidHeader, uid);
        if (json is not null) request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return _client.SendAsync(request, Ct);
    }

    /// <summary>A complete list where every type is granted unless listed in <paramref name="refused"/>.</summary>
    private static string Body(params string[] refused) => JsonSerializer.Serialize(
        AllTypes.Select(t => new { type = t, granted = !refused.Contains(t) }));

    private async Task<Dictionary<string, bool>> ReadMapAsync(HttpResponseMessage response)
    {
        var list = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return list.EnumerateArray().ToDictionary(i => i.GetProperty("type").GetString()!, i => i.GetProperty("granted").GetBoolean());
    }

    // ---- Authentication and role ----

    [Fact]
    public async Task Get_without_token_returns_401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync("/api/consents", null)).StatusCode);
    }

    [Fact]
    public async Task Put_without_token_returns_401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await PutAsync(null, Body())).StatusCode);
    }

    [Fact]
    public async Task Circle_consents_without_token_returns_401()
    {
        var response = await GetAsync($"/api/circles/{Guid.NewGuid()}/consents", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_caregiver_cannot_read_or_write_own_consents()
    {
        await SeedUserAsync("cg", AuthRole.Caregiver);

        Assert.Equal(HttpStatusCode.Forbidden, (await GetAsync("/api/consents", "cg")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PutAsync("cg", Body())).StatusCode);
    }

    [Fact]
    public async Task A_token_without_a_local_user_is_forbidden()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await GetAsync("/api/consents", "ghost")).StatusCode);
    }

    // ---- GET /api/consents ----

    [Fact]
    public async Task Get_returns_the_defaults_before_anything_is_saved()
    {
        await SeedUserAsync("elder", AuthRole.Elder);

        var response = await GetAsync("/api/consents", "elder");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var map = await ReadMapAsync(response);
        Assert.Equal(AllTypes.OrderBy(t => t), map.Keys.OrderBy(t => t));
        Assert.All(map.Where(kv => kv.Key != "Ecg"), kv => Assert.True(kv.Value));
        Assert.False(map["Ecg"]);
    }

    [Fact]
    public async Task Get_does_not_write_the_defaults_to_the_database()
    {
        await SeedUserAsync("elder", AuthRole.Elder);

        await GetAsync("/api/consents", "elder");

        using var _ = _factory.CreateDbScope(out var db);
        Assert.Empty(db.Consents);
    }

    // ---- PUT /api/consents: happy path ----

    [Fact]
    public async Task Put_stores_the_choices_and_get_returns_them()
    {
        await SeedUserAsync("elder", AuthRole.Elder);

        var put = await PutAsync("elder", Body("BloodPressure", "Spo2"));

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var map = await ReadMapAsync(await GetAsync("/api/consents", "elder"));
        Assert.False(map["BloodPressure"]);
        Assert.False(map["Spo2"]);
        Assert.True(map["Ecg"]);
        Assert.True(map["HeartRate"]);
    }

    [Fact]
    public async Task Put_is_isolated_per_user()
    {
        await SeedUserAsync("elder-1", AuthRole.Elder);
        await SeedUserAsync("elder-2", AuthRole.Elder);

        await PutAsync("elder-1", Body("Ecg", "Spo2"));

        var other = await ReadMapAsync(await GetAsync("/api/consents", "elder-2"));
        Assert.True(other["Spo2"]);
    }

    [Fact]
    public async Task Put_records_when_a_consent_is_granted_and_withdrawn()
    {
        await SeedUserAsync("elder", AuthRole.Elder);

        await PutAsync("elder", Body("Spo2"));
        await PutAsync("elder", Body());
        await PutAsync("elder", Body("Spo2"));

        using var _ = _factory.CreateDbScope(out var db);
        var spo2 = await db.Consents.SingleAsync(c => c.UserId == "elder" && c.Type == HealthDataType.Spo2, Ct);
        Assert.False(spo2.Granted);
        Assert.NotNull(spo2.GrantedAt);
        Assert.NotNull(spo2.RevokedAt);
        Assert.Equal(7, await db.Consents.CountAsync(c => c.UserId == "elder", Ct));
    }

    [Fact]
    public async Task Put_without_changes_keeps_the_timestamps()
    {
        await SeedUserAsync("elder", AuthRole.Elder);
        await PutAsync("elder", Body("Ecg"));
        DateTimeOffset before;
        using (var _ = _factory.CreateDbScope(out var db))
            before = (await db.Consents.SingleAsync(c => c.Type == HealthDataType.Ecg, Ct)).UpdatedAt;

        await PutAsync("elder", Body("Ecg"));

        using var __ = _factory.CreateDbScope(out var db2);
        Assert.Equal(before, (await db2.Consents.SingleAsync(c => c.Type == HealthDataType.Ecg, Ct)).UpdatedAt);
    }

    // ---- PUT /api/consents: input validation ----

    [Theory]
    [InlineData("HeartRate")]
    [InlineData("Location")]
    [InlineData("Sleep")]
    [InlineData("ScreenTime")]
    public async Task Put_refusing_a_required_type_returns_400(string type)
    {
        await SeedUserAsync("elder", AuthRole.Elder);

        var response = await PutAsync("elder", Body(type));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors");
        Assert.Contains(errors.EnumerateObject(), e => e.Name.EndsWith(".granted"));
    }

    [Fact]
    public async Task Put_refusing_every_required_type_is_rejected_and_nothing_is_saved()
    {
        await SeedUserAsync("elder", AuthRole.Elder);

        var response = await PutAsync("elder", Body(RequiredTypes));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var _ = _factory.CreateDbScope(out var db);
        Assert.Empty(db.Consents);
    }

    [Fact]
    public async Task Put_with_a_missing_type_returns_400()
    {
        await SeedUserAsync("elder", AuthRole.Elder);
        var body = JsonSerializer.Serialize(AllTypes.Skip(1).Select(t => new { type = t, granted = true }));

        var response = await PutAsync("elder", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Contains("HeartRate", problem.GetProperty("errors").GetProperty("items")[0].GetString());
    }

    [Fact]
    public async Task Put_with_a_duplicated_type_returns_400()
    {
        await SeedUserAsync("elder", AuthRole.Elder);
        var body = JsonSerializer.Serialize(AllTypes.Append("Ecg").Select(t => new { type = t, granted = true }));

        Assert.Equal(HttpStatusCode.BadRequest, (await PutAsync("elder", body)).StatusCode);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("\"HeartRate\"")]
    [InlineData("not json")]
    [InlineData("")]
    public async Task Put_with_a_malformed_body_returns_400(string body)
    {
        await SeedUserAsync("elder", AuthRole.Elder);

        var response = await PutAsync("elder", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("""[{"type":"Unknown","granted":true}]""")]
    [InlineData("""[{"type":"heart_rate","granted":true}]""")]
    [InlineData("""[{"type":99,"granted":true}]""")]
    [InlineData("""[{"type":null,"granted":true}]""")]
    [InlineData("""[{"granted":true}]""")]
    [InlineData("""[{"type":"Ecg"}]""")]
    [InlineData("""[{"type":"Ecg","granted":"yes"}]""")]
    [InlineData("[null]")]
    public async Task Put_with_an_invalid_item_returns_400(string body)
    {
        await SeedUserAsync("elder", AuthRole.Elder);

        var response = await PutAsync("elder", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var _ = _factory.CreateDbScope(out var db);
        Assert.Empty(db.Consents);
    }

    // ---- GET /api/circles/{id}/consents ----

    [Theory]
    [InlineData("elder")]
    [InlineData("admin")]
    [InlineData("cg")]
    public async Task Members_can_read_the_owner_consents(string reader)
    {
        var id = await SeedCircleAsync("elder", ("admin", MemberRole.Admin), ("cg", MemberRole.Caregiver));
        await PutAsync("elder", Body("Ecg", "Spo2"));

        var response = await GetAsync($"/api/circles/{id}/consents", reader);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var map = await ReadMapAsync(response);
        Assert.False(map["Spo2"]);
        Assert.False(map["Ecg"]);
        Assert.True(map["HeartRate"]);
    }

    [Fact]
    public async Task Circle_consents_default_when_the_owner_saved_nothing()
    {
        var id = await SeedCircleAsync("elder", ("cg", MemberRole.Caregiver));

        var map = await ReadMapAsync(await GetAsync($"/api/circles/{id}/consents", "cg"));

        Assert.True(map["Spo2"]);
        Assert.False(map["Ecg"]);
    }

    [Fact]
    public async Task Circle_consents_for_a_non_member_returns_404()
    {
        var id = await SeedCircleAsync("elder");
        await SeedUserAsync("stranger", AuthRole.Caregiver);

        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync($"/api/circles/{id}/consents", "stranger")).StatusCode);
    }

    [Fact]
    public async Task Circle_consents_for_an_unknown_circle_returns_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync($"/api/circles/{Guid.NewGuid()}/consents", "anyone")).StatusCode);
    }

    [Fact]
    public async Task Circle_consents_with_a_malformed_id_returns_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync("/api/circles/not-a-guid/consents", "anyone")).StatusCode);
    }

    [Fact]
    public async Task A_caregiver_cannot_change_the_owner_consents()
    {
        await SeedCircleAsync("elder", ("cg", MemberRole.Caregiver));

        var response = await PutAsync("cg", Body("Ecg"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var _ = _factory.CreateDbScope(out var db);
        Assert.Empty(db.Consents);
    }
}
