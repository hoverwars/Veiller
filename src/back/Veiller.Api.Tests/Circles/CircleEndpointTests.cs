using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Veiller.Api.Data.Entities;
using Veiller.Api.Tests.Infrastructure;
using AuthRole = Veiller.Api.Auth.Role;

namespace Veiller.Api.Tests.Circles;

public sealed class CircleEndpointTests : IDisposable
{
    private readonly VeillerApiFactory _factory = new();
    private readonly HttpClient _client;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public CircleEndpointTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // ---- Helpers ----

    private async Task<Guid> SeedCircleAsync(string ownerId, params (string UserId, MemberRole Role)[] others)
    {
        using var _ = _factory.CreateDbScope(out var db);
        var now = DateTimeOffset.UtcNow;
        foreach (var uid in others.Select(o => o.UserId).Prepend(ownerId).Distinct())
        {
            if (await db.Users.AnyAsync(x => x.Id == uid, Ct)) continue;
            db.Users.Add(new User { Id = uid, Email = $"{uid}@example.com", FullName = $"Name {uid}", Role = AuthRole.Caregiver, CreatedAt = now });
            await db.SaveChangesAsync(Ct);
        }

        var circle = new Circle { OwnerUserId = ownerId, CreatedAt = now };
        circle.Members.Add(new CircleMember { CircleId = circle.Id, UserId = ownerId, Role = MemberRole.DataOwner, JoinedAt = now });
        foreach (var (uid, role) in others)
            circle.Members.Add(new CircleMember { CircleId = circle.Id, UserId = uid, Role = role, JoinedAt = now.AddMinutes(1) });
        db.Circles.Add(circle);
        await db.SaveChangesAsync(Ct);
        return circle.Id;
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string? uid)
    {
        var request = new HttpRequestMessage(method, url);
        if (uid is not null) request.Headers.Add(TestAuthHandler.UidHeader, uid);
        return _client.SendAsync(request, Ct);
    }

    private Task<HttpResponseMessage> GetAsync(string url, string? uid) => SendAsync(HttpMethod.Get, url, uid);

    private Task<HttpResponseMessage> DeleteAsync(string url, string? uid) => SendAsync(HttpMethod.Delete, url, uid);

    private async Task<int> MemberCountAsync(Guid circleId)
    {
        using var _ = _factory.CreateDbScope(out var db);
        return await db.CircleMembers.CountAsync(m => m.CircleId == circleId, Ct);
    }

    // ---- Authentication ----

    [Theory]
    [InlineData("/api/circles")]
    [InlineData("/api/circles/own")]
    [InlineData("/api/circles/6f9619ff-8b86-d011-b42d-00c04fc964ff")]
    public async Task Get_without_token_returns_401(string url)
    {
        var response = await GetAsync(url, uid: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Delete_member_without_token_returns_401()
    {
        var response = await DeleteAsync($"/api/circles/{Guid.NewGuid()}/members/someone", uid: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- GET /api/circles/{id} ----

    [Fact]
    public async Task Get_circle_as_member_returns_members_and_owner()
    {
        var id = await SeedCircleAsync("elder", ("admin", MemberRole.Admin), ("cg", MemberRole.Caregiver));

        var response = await GetAsync($"/api/circles/{id}", "cg");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("elder", json.GetProperty("owner").GetProperty("userId").GetString());
        Assert.Equal("DataOwner", json.GetProperty("owner").GetProperty("role").GetString());
        Assert.Equal("Free", json.GetProperty("plan").GetString());
        Assert.Equal(3, json.GetProperty("members").GetArrayLength());
        Assert.Equal(0, json.GetProperty("pendingRequests").GetArrayLength());
    }

    [Fact]
    public async Task Get_circle_computes_the_owner_age_from_the_birth_date()
    {
        var id = await SeedCircleAsync("elder");
        using (var scope = _factory.CreateDbScope(out var db))
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            (await db.Users.SingleAsync(u => u.Id == "elder", Ct)).BirthDate = today.AddYears(-80).AddDays(-1);
            await db.SaveChangesAsync(Ct);
        }

        var json = await (await GetAsync($"/api/circles/{id}", "elder")).Content.ReadFromJsonAsync<JsonElement>(Ct);

        Assert.Equal(80, json.GetProperty("ownerAge").GetInt32());
    }

    [Fact]
    public async Task Get_circle_as_non_member_returns_404()
    {
        var id = await SeedCircleAsync("elder");

        var response = await GetAsync($"/api/circles/{id}", "stranger");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_unknown_circle_returns_404()
    {
        var response = await GetAsync($"/api/circles/{Guid.NewGuid()}", "anyone");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_circle_with_a_malformed_id_returns_404()
    {
        var response = await GetAsync("/api/circles/not-a-guid", "anyone");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- GET /api/circles and /own ----

    [Fact]
    public async Task List_returns_only_circles_where_the_user_is_a_caregiver()
    {
        var mine = await SeedCircleAsync("elder-1", ("cg", MemberRole.Caregiver));
        await SeedCircleAsync("elder-2");
        await SeedCircleAsync("cg"); // cg owns this one: it is not a caregiver circle

        var response = await GetAsync("/api/circles", "cg");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var item = Assert.Single(json.EnumerateArray());
        Assert.Equal(mine, item.GetProperty("id").GetGuid());
        Assert.Equal(1, item.GetProperty("caregiverCount").GetInt32());
    }

    [Fact]
    public async Task List_for_a_user_without_circles_returns_an_empty_array()
    {
        var json = await (await GetAsync("/api/circles", "nobody")).Content.ReadFromJsonAsync<JsonElement>(Ct);

        Assert.Equal(0, json.GetArrayLength());
    }

    [Fact]
    public async Task Own_returns_the_circle_of_the_elder()
    {
        var id = await SeedCircleAsync("elder");

        var response = await GetAsync("/api/circles/own", "elder");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(id, json.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Own_without_a_circle_returns_404()
    {
        var response = await GetAsync("/api/circles/own", "caregiver-without-circle");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- DELETE /api/circles/{id}/members/{userId} ----

    [Fact]
    public async Task Admin_can_remove_a_caregiver()
    {
        var id = await SeedCircleAsync("elder", ("admin", MemberRole.Admin), ("cg", MemberRole.Caregiver));

        var response = await DeleteAsync($"/api/circles/{id}/members/cg", "admin");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(2, await MemberCountAsync(id));
    }

    [Fact]
    public async Task Owner_can_remove_a_caregiver()
    {
        var id = await SeedCircleAsync("elder", ("cg", MemberRole.Caregiver));

        var response = await DeleteAsync($"/api/circles/{id}/members/cg", "elder");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Caregiver_can_remove_themselves()
    {
        var id = await SeedCircleAsync("elder", ("cg", MemberRole.Caregiver));

        var response = await DeleteAsync($"/api/circles/{id}/members/cg", "cg");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(1, await MemberCountAsync(id));
    }

    [Fact]
    public async Task Caregiver_cannot_remove_another_member()
    {
        var id = await SeedCircleAsync("elder", ("cg", MemberRole.Caregiver), ("other", MemberRole.Caregiver));

        var response = await DeleteAsync($"/api/circles/{id}/members/other", "cg");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(3, await MemberCountAsync(id));
    }

    [Theory]
    [InlineData("elder")]
    [InlineData("admin")]
    public async Task The_owner_can_never_be_removed(string actor)
    {
        var id = await SeedCircleAsync("elder", ("admin", MemberRole.Admin));

        var response = await DeleteAsync($"/api/circles/{id}/members/elder", actor);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(2, await MemberCountAsync(id));
    }

    [Fact]
    public async Task Removing_an_unknown_member_returns_404()
    {
        var id = await SeedCircleAsync("elder", ("admin", MemberRole.Admin));

        var response = await DeleteAsync($"/api/circles/{id}/members/ghost", "admin");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Removing_a_member_as_a_non_member_returns_404()
    {
        var id = await SeedCircleAsync("elder", ("cg", MemberRole.Caregiver));

        var response = await DeleteAsync($"/api/circles/{id}/members/cg", "stranger");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(2, await MemberCountAsync(id));
    }

    [Fact]
    public async Task A_circle_cannot_have_two_admins()
    {
        var id = await SeedCircleAsync("elder", ("admin-1", MemberRole.Admin));

        using var _ = _factory.CreateDbScope(out var db);
        db.Users.Add(new User { Id = "admin-2", Email = "a2@example.com", FullName = "Admin Two", Role = AuthRole.Caregiver });
        db.CircleMembers.Add(new CircleMember { CircleId = id, UserId = "admin-2", Role = MemberRole.Admin });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
    }
}
