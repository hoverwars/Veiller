using System.Net;
using Microsoft.EntityFrameworkCore;
using Veiller.Api.Auth;
using Veiller.Api.Data.Entities;
using Veiller.Api.Tests.Infrastructure;

namespace Veiller.Api.Tests.Circles;

/// <summary>The circle of an elder is created, once, when the onboarding is completed.</summary>
public sealed class OwnCircleCreationTests : IDisposable
{
    private const string Url = "/api/auth/me/onboarding";

    private readonly VeillerApiFactory _factory = new();
    private readonly HttpClient _client;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public OwnCircleCreationTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task<HttpResponseMessage> CompleteOnboardingAsync(string uid, string role)
    {
        _factory.Admin.Users[uid] = new FirebaseUser(uid, "Jane Doe", $"{uid}@example.com",
            new Dictionary<string, object> { ["role"] = role });
        using (var scope = _factory.CreateDbScope(out var db))
        {
            db.Users.Add(new User
            {
                Id = uid,
                Email = $"{uid}@example.com",
                FullName = "Jane Doe",
                Role = role == "elder" ? Role.Elder : Role.Caregiver,
            });
            await db.SaveChangesAsync(Ct);
        }

        var request = new HttpRequestMessage(HttpMethod.Post, Url);
        request.Headers.Add(TestAuthHandler.UidHeader, uid);
        return await _client.SendAsync(request, Ct);
    }

    [Fact]
    public async Task Onboarding_of_an_elder_creates_their_circle_with_them_as_data_owner()
    {
        var response = await CompleteOnboardingAsync("elder", "elder");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var _ = _factory.CreateDbScope(out var db);
        var circle = await db.Circles.Include(c => c.Members).SingleAsync(Ct);
        Assert.Equal("elder", circle.OwnerUserId);
        Assert.Equal(PlanTier.Free, circle.Plan);
        var member = Assert.Single(circle.Members);
        Assert.Equal(MemberRole.DataOwner, member.Role);
    }

    [Fact]
    public async Task Completing_the_onboarding_twice_does_not_create_a_second_circle()
    {
        await CompleteOnboardingAsync("elder", "elder");
        var request = new HttpRequestMessage(HttpMethod.Post, Url);
        request.Headers.Add(TestAuthHandler.UidHeader, "elder");

        var response = await _client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var _ = _factory.CreateDbScope(out var db);
        Assert.Equal(1, await db.Circles.CountAsync(Ct));
    }

    [Fact]
    public async Task Onboarding_of_a_caregiver_does_not_create_a_circle()
    {
        var response = await CompleteOnboardingAsync("cg", "caregiver");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var _ = _factory.CreateDbScope(out var db);
        Assert.Equal(0, await db.Circles.CountAsync(Ct));
    }
}
