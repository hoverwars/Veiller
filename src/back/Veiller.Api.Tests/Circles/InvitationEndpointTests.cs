using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Veiller.Api.Circles.Invitations;
using Veiller.Api.Data.Entities;
using Veiller.Api.Tests.Infrastructure;
using AuthRole = Veiller.Api.Auth.Role;

namespace Veiller.Api.Tests.Circles;

public sealed class InvitationEndpointTests : IDisposable
{
    private const string Owner = "owner";

    private readonly VeillerApiFactory _factory = new();
    private readonly HttpClient _client;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public InvitationEndpointTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // ---- Helpers ----

    private async Task SeedUserAsync(string uid, AuthRole role = AuthRole.Caregiver)
    {
        using var _ = _factory.CreateDbScope(out var db);
        if (await db.Users.AnyAsync(x => x.Id == uid, Ct)) return;
        db.Users.Add(new User
        {
            Id = uid, Email = $"{uid}@example.com", FullName = $"Name {uid}", Role = role, CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(Ct);
    }

    private async Task<Guid> SeedCircleAsync(
        PlanTier plan = PlanTier.Free, params (string UserId, MemberRole Role)[] others)
    {
        await SeedUserAsync(Owner, AuthRole.Elder);
        foreach (var (uid, _) in others) await SeedUserAsync(uid);

        using var _ = _factory.CreateDbScope(out var db);
        var now = DateTimeOffset.UtcNow;
        var circle = new Circle { OwnerUserId = Owner, Plan = plan, CreatedAt = now };
        circle.Members.Add(new CircleMember { CircleId = circle.Id, UserId = Owner, Role = MemberRole.DataOwner, JoinedAt = now });
        foreach (var (uid, role) in others)
            circle.Members.Add(new CircleMember { CircleId = circle.Id, UserId = uid, Role = role, IsSponsor = role == MemberRole.Admin, JoinedAt = now });
        db.Circles.Add(circle);
        await db.SaveChangesAsync(Ct);
        return circle.Id;
    }

    private async Task SeedCodeAsync(Guid circleId, string code, TimeSpan validFor)
    {
        using var _ = _factory.CreateDbScope(out var db);
        var now = DateTimeOffset.UtcNow;
        db.InviteCodes.Add(new InviteCode { CircleId = circleId, Code = code, CreatedAt = now.AddHours(-30), ExpiresAt = now + validFor });
        await db.SaveChangesAsync(Ct);
    }

    private async Task<Guid> SeedRequestAsync(Guid circleId, string userId)
    {
        await SeedUserAsync(userId);
        using var _ = _factory.CreateDbScope(out var db);
        var request = new JoinRequest { CircleId = circleId, UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
        db.JoinRequests.Add(request);
        await db.SaveChangesAsync(Ct);
        return request.Id;
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string? uid, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (uid is not null) request.Headers.Add(TestAuthHandler.UidHeader, uid);
        if (body is not null) request.Content = JsonContent.Create(body);
        return _client.SendAsync(request, Ct);
    }

    private Task<HttpResponseMessage> PostAsync(string url, string? uid, object? body = null) =>
        SendAsync(HttpMethod.Post, url, uid, body);

    private Task<HttpResponseMessage> GetAsync(string url, string? uid) => SendAsync(HttpMethod.Get, url, uid);

    private async Task<string> GenerateCodeAsync(Guid circleId)
    {
        var response = await PostAsync($"/api/circles/{circleId}/invite-code", Owner);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return json.RootElement.GetProperty("code").GetString()!;
    }

    private async Task<List<MemberRole>> RolesOfAsync(Guid circleId)
    {
        using var _ = _factory.CreateDbScope(out var db);
        return await db.CircleMembers.Where(m => m.CircleId == circleId).Select(m => m.Role).ToListAsync(Ct);
    }

    // ---- Code format ----

    [Theory]
    [InlineData("vlr-7k2p", "VLR-7K2P")]
    [InlineData("VLR7K2P", "VLR-7K2P")]
    [InlineData("  7k2p ", "VLR-7K2P")]
    [InlineData("vlr 7k2p", "VLR-7K2P")]
    public void Normalize_accepts_loose_input(string input, string expected) =>
        Assert.Equal(expected, InviteCodeFormat.Normalize(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("VLR-")]
    [InlineData("VLR-ABC")]
    [InlineData("VLR-ABCDE")]
    [InlineData("VLR-0O1I")] // characters excluded from the alphabet
    public void Normalize_rejects_malformed_input(string? input) =>
        Assert.Null(InviteCodeFormat.Normalize(input));

    [Fact]
    public void Generated_codes_are_well_formed()
    {
        for (var i = 0; i < 200; i++)
        {
            var code = InviteCodeFormat.Generate();
            Assert.Equal(code, InviteCodeFormat.Normalize(code));
        }
    }

    // ---- Authentication ----

    [Theory]
    [InlineData("POST", "/api/circles/6f9619ff-8b86-d011-b42d-00c04fc964ff/invite-code")]
    [InlineData("GET", "/api/circles/by-code/VLR-7K2P")]
    [InlineData("POST", "/api/circles/join-requests")]
    [InlineData("POST", "/api/circles/6f9619ff-8b86-d011-b42d-00c04fc964ff/requests/6f9619ff-8b86-d011-b42d-00c04fc964ff/approve")]
    [InlineData("POST", "/api/circles/6f9619ff-8b86-d011-b42d-00c04fc964ff/requests/6f9619ff-8b86-d011-b42d-00c04fc964ff/reject")]
    public async Task Endpoints_without_token_return_401(string method, string url)
    {
        var response = await SendAsync(new HttpMethod(method), url, uid: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- Generate code ----

    [Fact]
    public async Task Owner_generates_a_code_valid_for_24_hours()
    {
        var circleId = await SeedCircleAsync();

        var response = await PostAsync($"/api/circles/{circleId}/invite-code", Owner);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var code = json.RootElement.GetProperty("code").GetString();
        Assert.Equal(code, InviteCodeFormat.Normalize(code));
        var expiresAt = json.RootElement.GetProperty("expiresAt").GetDateTimeOffset();
        Assert.InRange(expiresAt - DateTimeOffset.UtcNow, TimeSpan.FromHours(23.9), TimeSpan.FromHours(24.1));
    }

    [Fact]
    public async Task Admin_can_generate_a_code()
    {
        var circleId = await SeedCircleAsync(PlanTier.Free, ("admin", MemberRole.Admin));

        var response = await PostAsync($"/api/circles/{circleId}/invite-code", "admin");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Plain_caregiver_cannot_generate_a_code()
    {
        var circleId = await SeedCircleAsync(PlanTier.Free, ("cg", MemberRole.Caregiver));

        var response = await PostAsync($"/api/circles/{circleId}/invite-code", "cg");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Non_member_generating_a_code_gets_404()
    {
        var circleId = await SeedCircleAsync();
        await SeedUserAsync("stranger");

        var response = await PostAsync($"/api/circles/{circleId}/invite-code", "stranger");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Generating_a_new_code_invalidates_the_previous_one()
    {
        var circleId = await SeedCircleAsync();
        await SeedUserAsync("newcomer");
        var first = await GenerateCodeAsync(circleId);
        var second = await GenerateCodeAsync(circleId);

        Assert.NotEqual(first, second);
        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync($"/api/circles/by-code/{first}", "newcomer")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await GetAsync($"/api/circles/by-code/{second}", "newcomer")).StatusCode);
    }

    // ---- Preview ----

    [Fact]
    public async Task Preview_returns_circle_summary_and_accepts_loose_input()
    {
        var circleId = await SeedCircleAsync(PlanTier.Free, ("admin", MemberRole.Admin), ("cg", MemberRole.Caregiver));
        await SeedUserAsync("newcomer");
        var code = await GenerateCodeAsync(circleId);

        var response = await GetAsync($"/api/circles/by-code/{code.ToLowerInvariant().Replace("-", "")}", "newcomer");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var root = json.RootElement;
        Assert.Equal(code, root.GetProperty("code").GetString());
        Assert.Equal("Name owner", root.GetProperty("ownerName").GetString());
        Assert.Equal("Name admin", root.GetProperty("adminName").GetString());
        Assert.Equal("Free", root.GetProperty("plan").GetString());
        Assert.Equal(2, root.GetProperty("caregiverCount").GetInt32());
    }

    [Fact]
    public async Task Unknown_and_expired_codes_return_the_same_404()
    {
        var circleId = await SeedCircleAsync();
        await SeedUserAsync("newcomer");
        await SeedCodeAsync(circleId, "VLR-ABCD", TimeSpan.FromHours(-1));

        var expired = await GetAsync("/api/circles/by-code/VLR-ABCD", "newcomer");
        var unknown = await GetAsync("/api/circles/by-code/VLR-WXYZ", "newcomer");

        Assert.Equal(HttpStatusCode.NotFound, expired.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        // Same body apart from the per-request trace id.
        static string Detail(string body) => JsonDocument.Parse(body).RootElement.GetProperty("detail").GetString()!;
        Assert.Equal(Detail(await unknown.Content.ReadAsStringAsync(Ct)), Detail(await expired.Content.ReadAsStringAsync(Ct)));
    }

    [Theory]
    [InlineData("VLR-ABC")]
    [InlineData("VLR-ABCDE")]
    [InlineData("VLR-0000")]
    [InlineData("hello")]
    public async Task Preview_with_malformed_code_returns_400(string code)
    {
        await SeedUserAsync("newcomer");

        var response = await GetAsync($"/api/circles/by-code/{code}", "newcomer");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- Join request ----

    [Fact]
    public async Task Caregiver_can_request_to_join_and_owner_sees_the_pending_request()
    {
        var circleId = await SeedCircleAsync();
        await SeedUserAsync("newcomer");
        var code = await GenerateCodeAsync(circleId);

        var response = await PostAsync("/api/circles/join-requests", "newcomer", new { code = code.ToLowerInvariant() });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var detail = await GetAsync($"/api/circles/{circleId}", Owner);
        using var json = JsonDocument.Parse(await detail.Content.ReadAsStringAsync(Ct));
        var pending = json.RootElement.GetProperty("pendingRequests");
        Assert.Equal(1, pending.GetArrayLength());
        Assert.Equal("Name newcomer", pending[0].GetProperty("fullName").GetString());
    }

    [Fact]
    public async Task Code_is_reusable_by_several_caregivers()
    {
        var circleId = await SeedCircleAsync();
        await SeedUserAsync("a");
        await SeedUserAsync("b");
        var code = await GenerateCodeAsync(circleId);

        Assert.Equal(HttpStatusCode.Accepted, (await PostAsync("/api/circles/join-requests", "a", new { code })).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await PostAsync("/api/circles/join-requests", "b", new { code })).StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("VLR-12")]
    [InlineData("VLR-0O1I")]
    public async Task Join_with_missing_or_malformed_code_returns_400(string? code)
    {
        await SeedUserAsync("newcomer");

        var response = await PostAsync("/api/circles/join-requests", "newcomer", new { code });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Join_without_body_returns_400()
    {
        await SeedUserAsync("newcomer");

        var response = await PostAsync("/api/circles/join-requests", "newcomer", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Join_with_unknown_code_returns_404()
    {
        await SeedUserAsync("newcomer");

        var response = await PostAsync("/api/circles/join-requests", "newcomer", new { code = "VLR-WXYZ" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Join_with_expired_code_returns_404()
    {
        var circleId = await SeedCircleAsync();
        await SeedUserAsync("newcomer");
        await SeedCodeAsync(circleId, "VLR-ABCD", TimeSpan.FromMinutes(-1));

        var response = await PostAsync("/api/circles/join-requests", "newcomer", new { code = "VLR-ABCD" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_request_returns_409()
    {
        var circleId = await SeedCircleAsync();
        await SeedUserAsync("newcomer");
        var code = await GenerateCodeAsync(circleId);
        await PostAsync("/api/circles/join-requests", "newcomer", new { code });

        var response = await PostAsync("/api/circles/join-requests", "newcomer", new { code });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Existing_member_cannot_request_again()
    {
        var circleId = await SeedCircleAsync(PlanTier.Free, ("cg", MemberRole.Caregiver));
        var code = await GenerateCodeAsync(circleId);

        var response = await PostAsync("/api/circles/join-requests", "cg", new { code });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Elder_cannot_request_to_join()
    {
        var circleId = await SeedCircleAsync();
        await SeedUserAsync("other-elder", AuthRole.Elder);
        var code = await GenerateCodeAsync(circleId);

        var response = await PostAsync("/api/circles/join-requests", "other-elder", new { code });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- Approve / reject ----

    [Fact]
    public async Task First_approved_caregiver_becomes_admin_and_sponsor_then_others_are_caregivers()
    {
        var circleId = await SeedCircleAsync(PlanTier.Premium);
        var first = await SeedRequestAsync(circleId, "first");
        var second = await SeedRequestAsync(circleId, "second");

        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync($"/api/circles/{circleId}/requests/{first}/approve", Owner)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync($"/api/circles/{circleId}/requests/{second}/approve", Owner)).StatusCode);

        using var _ = _factory.CreateDbScope(out var db);
        var members = await db.CircleMembers.Where(m => m.CircleId == circleId).ToListAsync(Ct);
        var firstMember = members.Single(m => m.UserId == "first");
        Assert.Equal(MemberRole.Admin, firstMember.Role);
        Assert.True(firstMember.IsSponsor);
        var secondMember = members.Single(m => m.UserId == "second");
        Assert.Equal(MemberRole.Caregiver, secondMember.Role);
        Assert.False(secondMember.IsSponsor);
        Assert.Empty(await db.JoinRequests.Where(r => r.CircleId == circleId).ToListAsync(Ct));
    }

    [Fact]
    public async Task Admin_can_approve_a_request()
    {
        var circleId = await SeedCircleAsync(PlanTier.Free, ("admin", MemberRole.Admin));
        var request = await SeedRequestAsync(circleId, "newcomer");

        var response = await PostAsync($"/api/circles/{circleId}/requests/{request}/approve", "admin");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Contains(MemberRole.Caregiver, await RolesOfAsync(circleId));
    }

    [Fact]
    public async Task Approving_over_the_free_plan_limit_returns_409()
    {
        var circleId = await SeedCircleAsync(PlanTier.Free, ("admin", MemberRole.Admin), ("cg", MemberRole.Caregiver));
        var request = await SeedRequestAsync(circleId, "newcomer");

        var response = await PostAsync($"/api/circles/{circleId}/requests/{request}/approve", Owner);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Limite du plan atteinte", await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(3, (await RolesOfAsync(circleId)).Count);
    }

    [Fact]
    public async Task Premium_plan_allows_more_than_two_caregivers()
    {
        var circleId = await SeedCircleAsync(PlanTier.Premium, ("admin", MemberRole.Admin), ("cg", MemberRole.Caregiver));
        var request = await SeedRequestAsync(circleId, "newcomer");

        var response = await PostAsync($"/api/circles/{circleId}/requests/{request}/approve", Owner);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Reject_removes_the_request_without_adding_a_member()
    {
        var circleId = await SeedCircleAsync();
        var request = await SeedRequestAsync(circleId, "newcomer");

        var response = await PostAsync($"/api/circles/{circleId}/requests/{request}/reject", Owner);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Single(await RolesOfAsync(circleId));
        using var _ = _factory.CreateDbScope(out var db);
        Assert.Empty(await db.JoinRequests.ToListAsync(Ct));
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("reject")]
    public async Task Plain_caregiver_cannot_review_requests(string action)
    {
        var circleId = await SeedCircleAsync(PlanTier.Premium, ("cg", MemberRole.Caregiver));
        var request = await SeedRequestAsync(circleId, "newcomer");

        var response = await PostAsync($"/api/circles/{circleId}/requests/{request}/{action}", "cg");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("reject")]
    public async Task Non_member_reviewing_gets_404(string action)
    {
        var circleId = await SeedCircleAsync();
        var request = await SeedRequestAsync(circleId, "newcomer");
        await SeedUserAsync("stranger");

        var response = await PostAsync($"/api/circles/{circleId}/requests/{request}/{action}", "stranger");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("reject")]
    public async Task Reviewing_an_unknown_request_returns_404(string action)
    {
        var circleId = await SeedCircleAsync();

        var response = await PostAsync($"/api/circles/{circleId}/requests/{Guid.NewGuid()}/{action}", Owner);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Request_of_another_circle_cannot_be_reviewed()
    {
        var circleId = await SeedCircleAsync();
        var other = await SeedOtherCircleRequestAsync();

        var response = await PostAsync($"/api/circles/{circleId}/requests/{other}/approve", Owner);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<Guid> SeedOtherCircleRequestAsync()
    {
        await SeedUserAsync("owner2", AuthRole.Elder);
        await SeedUserAsync("applicant");
        using var _ = _factory.CreateDbScope(out var db);
        var now = DateTimeOffset.UtcNow;
        var circle = new Circle { OwnerUserId = "owner2", CreatedAt = now };
        circle.Members.Add(new CircleMember { CircleId = circle.Id, UserId = "owner2", Role = MemberRole.DataOwner, JoinedAt = now });
        var request = new JoinRequest { CircleId = circle.Id, UserId = "applicant", CreatedAt = now };
        db.Circles.Add(circle);
        db.JoinRequests.Add(request);
        await db.SaveChangesAsync(Ct);
        return request.Id;
    }

    // ---- Rate limiting ----

    [Fact]
    public async Task By_code_is_rate_limited_per_user()
    {
        using var limited = _factory.WithWebHostBuilder(b => b.UseSetting("RateLimiting:InvitationsPerMinute", "3"));
        using var client = limited.CreateClient();

        async Task<HttpStatusCode> Call(string uid)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/circles/by-code/VLR-WXYZ");
            request.Headers.Add(TestAuthHandler.UidHeader, uid);
            return (await client.SendAsync(request, Ct)).StatusCode;
        }

        for (var i = 0; i < 3; i++) Assert.Equal(HttpStatusCode.NotFound, await Call("guesser"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await Call("guesser"));
        Assert.Equal(HttpStatusCode.NotFound, await Call("someone-else"));
    }
}
