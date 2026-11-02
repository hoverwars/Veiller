using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Veiller.Api.Auth;
using Veiller.Api.Data.Entities;
using Veiller.Api.Tests.Infrastructure;
using AuthRole = Veiller.Api.Auth.Role;

namespace Veiller.Api.Tests.Profile;

public sealed class ProfileEndpointTests : IDisposable
{
    private readonly VeillerApiFactory _factory = new();
    private readonly HttpClient _client;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ProfileEndpointTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // ---- Helpers ----

    private async Task SeedUserAsync(string uid, AuthRole role, bool inFirebase = true)
    {
        using var _ = _factory.CreateDbScope(out var db);
        if (await db.Users.AnyAsync(u => u.Id == uid, Ct)) return;
        db.Users.Add(new User
        {
            Id = uid, Email = $"{uid}@example.com", FullName = $"Name {uid}", Role = role,
            Phone = "+33 6 12 34 56 78", Relation = "Hija", CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(Ct);
        if (inFirebase)
            _factory.Admin.Users[uid] = new FirebaseUser(uid, $"Name {uid}", $"{uid}@example.com", new Dictionary<string, object>());
    }

    private async Task<Guid> SeedCircleAsync(string ownerId, params (string UserId, MemberRole Role, bool Sponsor, int JoinedDaysAgo)[] others)
    {
        await SeedUserAsync(ownerId, AuthRole.Elder);
        foreach (var o in others) await SeedUserAsync(o.UserId, AuthRole.Caregiver);

        using var scope = _factory.CreateDbScope(out var db);
        var now = DateTimeOffset.UtcNow;
        var circle = new Circle { OwnerUserId = ownerId, CreatedAt = now };
        circle.Members.Add(new CircleMember { CircleId = circle.Id, UserId = ownerId, Role = MemberRole.DataOwner, JoinedAt = now.AddDays(-100) });
        foreach (var o in others)
            circle.Members.Add(new CircleMember
            {
                CircleId = circle.Id, UserId = o.UserId, Role = o.Role, IsSponsor = o.Sponsor, JoinedAt = now.AddDays(-o.JoinedDaysAgo),
            });
        db.Circles.Add(circle);
        await db.SaveChangesAsync(Ct);
        return circle.Id;
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string? uid, string? json = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (uid is not null) request.Headers.Add(TestAuthHandler.UidHeader, uid);
        if (json is not null) request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return _client.SendAsync(request, Ct);
    }

    private Task<HttpResponseMessage> PatchAsync(string? uid, string json) => SendAsync(HttpMethod.Patch, "/api/profile", uid, json);

    private async Task<User> LoadUserAsync(string uid)
    {
        using var _ = _factory.CreateDbScope(out var db);
        return await db.Users.AsNoTracking().SingleAsync(u => u.Id == uid, Ct);
    }

    // ---- Authentication ----

    [Theory]
    [InlineData("GET", "/api/profile")]
    [InlineData("PATCH", "/api/profile")]
    [InlineData("DELETE", "/api/profile")]
    [InlineData("GET", "/api/profile/export")]
    public async Task Every_route_without_token_returns_401(string method, string url)
    {
        var response = await SendAsync(new HttpMethod(method), url, null, method == "PATCH" ? """{"fullName":"Ana"}""" : null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/profile")]
    [InlineData("PATCH", "/api/profile")]
    [InlineData("GET", "/api/profile/export")]
    public async Task A_token_without_a_local_user_returns_404(string method, string url)
    {
        var response = await SendAsync(new HttpMethod(method), url, "ghost", method == "PATCH" ? """{"fullName":"Ana"}""" : null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- GET /api/profile ----

    [Fact]
    public async Task Get_returns_the_profile_of_the_caller()
    {
        await SeedUserAsync("cg", AuthRole.Caregiver);

        var response = await SendAsync(HttpMethod.Get, "/api/profile", "cg");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("cg", body.GetProperty("id").GetString());
        Assert.Equal("Name cg", body.GetProperty("fullName").GetString());
        Assert.Equal("cg@example.com", body.GetProperty("email").GetString());
        Assert.Equal("Caregiver", body.GetProperty("role").GetString());
        Assert.Equal("+33 6 12 34 56 78", body.GetProperty("phone").GetString());
        Assert.Equal("Hija", body.GetProperty("relation").GetString());
    }

    // ---- PATCH /api/profile: validation ----

    public static TheoryData<string> InvalidPatches => new()
    {
        "{}",
        """{"fullName":null}""",
        """{"fullName":"A"}""",
        """{"fullName":"   "}""",
        $$"""{"fullName":"{{new string('a', 101)}}"}""",
        """{"phone":"abc"}""",
        """{"phone":"123"}""",
        $$"""{"phone":"{{new string('1', 31)}}"}""",
        $$"""{"relation":"{{new string('a', 51)}}"}""",
        $$"""{"birthDate":"{{DateTime.UtcNow.AddDays(1):yyyy-MM-dd}}"}""",
        """{"birthDate":"1800-01-01"}""",
        """{"birthDate":"not-a-date"}""",
        """{"fullName":123}""",
    };

    [Theory]
    [MemberData(nameof(InvalidPatches))]
    public async Task Patch_with_invalid_input_returns_400_and_changes_nothing(string json)
    {
        await SeedUserAsync("cg", AuthRole.Caregiver);

        var response = await PatchAsync("cg", json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var user = await LoadUserAsync("cg");
        Assert.Equal("Name cg", user.FullName);
        Assert.Equal("+33 6 12 34 56 78", user.Phone);
        Assert.Equal("Hija", user.Relation);
        Assert.Null(user.BirthDate);
    }

    [Fact]
    public async Task Patch_with_malformed_json_returns_400()
    {
        await SeedUserAsync("cg", AuthRole.Caregiver);

        Assert.Equal(HttpStatusCode.BadRequest, (await PatchAsync("cg", "{not json")).StatusCode);
    }

    // ---- PATCH /api/profile: behaviour ----

    [Fact]
    public async Task Patch_updates_only_the_provided_fields()
    {
        await SeedUserAsync("cg", AuthRole.Caregiver);

        var response = await PatchAsync("cg", """{"fullName":"  Ana Gomez ","birthDate":"1990-05-17"}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("Ana Gomez", body.GetProperty("fullName").GetString());
        Assert.Equal("1990-05-17", body.GetProperty("birthDate").GetString());
        var user = await LoadUserAsync("cg");
        Assert.Equal("Ana Gomez", user.FullName);
        Assert.Equal(new DateOnly(1990, 5, 17), user.BirthDate);
        Assert.Equal("+33 6 12 34 56 78", user.Phone);
        Assert.Equal("Hija", user.Relation);
    }

    [Fact]
    public async Task Patch_with_empty_phone_and_relation_clears_them()
    {
        await SeedUserAsync("cg", AuthRole.Caregiver);

        var response = await PatchAsync("cg", """{"phone":"","relation":"  "}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await LoadUserAsync("cg");
        Assert.Null(user.Phone);
        Assert.Null(user.Relation);
    }

    [Fact]
    public async Task Patch_updates_the_phone_with_a_valid_number()
    {
        await SeedUserAsync("cg", AuthRole.Caregiver);

        await PatchAsync("cg", """{"phone":"+34 600 111 222"}""");

        Assert.Equal("+34 600 111 222", (await LoadUserAsync("cg")).Phone);
    }

    [Fact]
    public async Task Patch_renaming_updates_the_firebase_display_name()
    {
        await SeedUserAsync("cg", AuthRole.Caregiver);

        await PatchAsync("cg", """{"fullName":"Ana Gomez"}""");

        Assert.Equal("Ana Gomez", _factory.Admin.Users["cg"].DisplayName);
    }

    [Fact]
    public async Task Patch_cannot_target_another_user()
    {
        await SeedUserAsync("cg", AuthRole.Caregiver);
        await SeedUserAsync("other", AuthRole.Caregiver);

        await PatchAsync("cg", """{"id":"other","fullName":"Hacked Name"}""");

        Assert.Equal("Name other", (await LoadUserAsync("other")).FullName);
    }

    // ---- DELETE /api/profile ----

    [Fact]
    public async Task Delete_of_a_caregiver_leaves_the_circle_intact()
    {
        var circleId = await SeedCircleAsync("elder", ("cg", MemberRole.Caregiver, false, 5), ("cg2", MemberRole.Caregiver, false, 3));

        var response = await SendAsync(HttpMethod.Delete, "/api/profile", "cg");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var _ = _factory.CreateDbScope(out var db);
        Assert.False(await db.Users.AnyAsync(u => u.Id == "cg", Ct));
        Assert.False(await db.CircleMembers.AnyAsync(m => m.UserId == "cg", Ct));
        Assert.True(await db.Circles.AnyAsync(c => c.Id == circleId, Ct));
        Assert.Equal(2, await db.CircleMembers.CountAsync(m => m.CircleId == circleId, Ct));
        Assert.False(_factory.Admin.Users.ContainsKey("cg"));
    }

    [Fact]
    public async Task Delete_of_an_elder_erases_the_circle_and_the_consents()
    {
        var circleId = await SeedCircleAsync("elder", ("cg", MemberRole.Caregiver, false, 5));
        using (var scope = _factory.CreateDbScope(out var seed))
        {
            seed.Consents.Add(new Consent { UserId = "elder", Type = Veiller.Api.Consents.HealthDataType.HeartRate, Granted = true, UpdatedAt = DateTimeOffset.UtcNow });
            seed.JoinRequests.Add(new JoinRequest { CircleId = circleId, UserId = "cg", CreatedAt = DateTimeOffset.UtcNow });
            await seed.SaveChangesAsync(Ct);
        }

        var response = await SendAsync(HttpMethod.Delete, "/api/profile", "elder");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var _ = _factory.CreateDbScope(out var db);
        Assert.False(await db.Circles.AnyAsync(c => c.Id == circleId, Ct));
        Assert.False(await db.CircleMembers.AnyAsync(m => m.CircleId == circleId, Ct));
        Assert.False(await db.Consents.AnyAsync(Ct));
        Assert.False(await db.JoinRequests.AnyAsync(Ct));
        Assert.True(await db.Users.AnyAsync(u => u.Id == "cg", Ct));
        Assert.False(_factory.Admin.Users.ContainsKey("elder"));
    }

    [Fact]
    public async Task Delete_of_the_admin_promotes_the_longest_standing_caregiver_and_keeps_the_sponsor()
    {
        var circleId = await SeedCircleAsync("elder",
            ("admin", MemberRole.Admin, true, 20), ("newer", MemberRole.Caregiver, false, 2), ("older", MemberRole.Caregiver, false, 10));

        var response = await SendAsync(HttpMethod.Delete, "/api/profile", "admin");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var _ = _factory.CreateDbScope(out var db);
        var older = await db.CircleMembers.SingleAsync(m => m.UserId == "older", Ct);
        var newer = await db.CircleMembers.SingleAsync(m => m.UserId == "newer", Ct);
        Assert.Equal(MemberRole.Admin, older.Role);
        Assert.True(older.IsSponsor);
        Assert.Equal(MemberRole.Caregiver, newer.Role);
        Assert.Equal(1, await db.CircleMembers.CountAsync(m => m.CircleId == circleId && m.Role == MemberRole.Admin, Ct));
    }

    [Fact]
    public async Task Delete_of_the_only_admin_without_caregivers_leaves_a_circle_with_no_admin()
    {
        var circleId = await SeedCircleAsync("elder", ("admin", MemberRole.Admin, true, 20));

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Delete, "/api/profile", "admin")).StatusCode);

        using var _ = _factory.CreateDbScope(out var db);
        Assert.Equal(1, await db.CircleMembers.CountAsync(m => m.CircleId == circleId, Ct));
    }

    [Fact]
    public async Task Delete_when_firebase_fails_returns_502_and_a_retry_completes_it()
    {
        await SeedUserAsync("cg", AuthRole.Caregiver);
        _factory.Admin.DeleteFailure = new InvalidOperationException("Firebase unreachable");

        var first = await SendAsync(HttpMethod.Delete, "/api/profile", "cg");

        Assert.Equal(HttpStatusCode.BadGateway, first.StatusCode);
        _factory.Admin.DeleteFailure = null;

        var retry = await SendAsync(HttpMethod.Delete, "/api/profile", "cg");

        Assert.Equal(HttpStatusCode.NoContent, retry.StatusCode);
        Assert.False(_factory.Admin.Users.ContainsKey("cg"));
    }

    [Fact]
    public async Task Delete_of_an_unknown_account_returns_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Delete, "/api/profile", "ghost")).StatusCode);
    }

    // ---- GET /api/profile/export ----

    [Fact]
    public async Task Export_of_a_caregiver_lists_their_circles_and_no_consents()
    {
        var circleId = await SeedCircleAsync("elder", ("cg", MemberRole.Admin, true, 5));
        await SeedCircleAsync("elder2", ("other", MemberRole.Admin, true, 5));

        var response = await SendAsync(HttpMethod.Get, "/api/profile/export", "cg");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("cg", body.GetProperty("profile").GetProperty("id").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("consents").ValueKind);
        var circles = body.GetProperty("circles").EnumerateArray().ToList();
        var circle = Assert.Single(circles);
        Assert.Equal(circleId, circle.GetProperty("id").GetGuid());
        Assert.Equal("Admin", circle.GetProperty("myRole").GetString());
        Assert.Equal("Name elder", circle.GetProperty("ownerName").GetString());
        Assert.Equal(2, circle.GetProperty("members").GetArrayLength());
        Assert.DoesNotContain("other", body.GetRawText());
    }

    [Fact]
    public async Task Export_of_an_elder_includes_the_consents()
    {
        await SeedCircleAsync("elder");

        var response = await SendAsync(HttpMethod.Get, "/api/profile/export", "elder");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(7, body.GetProperty("consents").GetArrayLength());
        Assert.Equal(JsonValueKind.String, body.GetProperty("exportedAt").ValueKind);
    }
}
