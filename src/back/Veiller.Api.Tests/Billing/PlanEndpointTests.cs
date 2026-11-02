using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Veiller.Api.Data.Entities;
using Veiller.Api.Tests.Patients;
using AuthRole = Veiller.Api.Auth.Role;

namespace Veiller.Api.Tests.Billing;

public sealed class PlanEndpointTests : PatientApiTestBase
{
    private const string Sponsor = "sponsor-1";
    private const string Second = "caregiver-2";
    private const string Third = "caregiver-3";
    private Guid _circle;

    public PlanEndpointTests()
    {
        _circle = SeedPlanCircleAsync(Elder, Sponsor, Second, Third).GetAwaiter().GetResult();
        Factory.Purchases.ValidTokens.Add("good-token");
        Factory.Purchases.ValidTokens.Add("other-good-token");
    }

    /// <summary>The first caregiver is admin and sponsor (like the first approved one); the others join one minute apart after them.</summary>
    private async Task<Guid> SeedPlanCircleAsync(string elder, string sponsor, params string[] others)
    {
        var id = await SeedCircleAsync(elder, [sponsor, .. others]);
        using var _ = Factory.CreateDbScope(out var db);
        var member = await db.CircleMembers.SingleAsync(m => m.CircleId == id && m.UserId == sponsor, Ct);
        member.Role = MemberRole.Admin;
        member.IsSponsor = true;
        await db.SaveChangesAsync(Ct);
        return id;
    }

    private Task<HttpResponseMessage> Post(string action, string? uid, object? body = null, Guid? circle = null) =>
        SendRawAsync(HttpMethod.Post, $"/api/circles/{circle ?? _circle}/plan/{action}", uid, body is null ? null : Json(body));

    private Task<HttpResponseMessage> Subscribe(string? uid, string? token = "good-token", Guid? circle = null) =>
        Post("subscribe", uid, new { purchaseToken = token }, circle);

    private async Task<(PlanTier Plan, string? Token, List<string> Members)> StateAsync(Guid? circle = null)
    {
        using var _ = Factory.CreateDbScope(out var db);
        var c = await db.Circles.Include(x => x.Members).SingleAsync(x => x.Id == (circle ?? _circle), Ct);
        return (c.Plan, c.PlanPurchaseToken, c.Members.Where(m => m.Role != MemberRole.DataOwner).Select(m => m.UserId).Order().ToList());
    }

    // ---- Subscribe ----

    [Fact]
    public async Task The_sponsor_subscribes_with_a_valid_purchase()
    {
        var response = await Subscribe(Sponsor);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("Premium", body.GetProperty("plan").GetString());
        Assert.Equal(0, body.GetProperty("removedMembers").GetArrayLength());
        Assert.Equal((PlanTier.Premium, "good-token"), ((await StateAsync()).Plan, (await StateAsync()).Token));
    }

    [Fact]
    public async Task Subscribing_again_with_the_same_purchase_is_harmless()
    {
        await Subscribe(Sponsor);

        Assert.Equal(HttpStatusCode.OK, (await Subscribe(Sponsor)).StatusCode);
        Assert.Equal(PlanTier.Premium, (await StateAsync()).Plan);
    }

    [Fact]
    public async Task The_circle_reports_its_new_plan()
    {
        await Subscribe(Sponsor);
        var response = await GetAsync($"/api/circles/{_circle}", Sponsor);

        var circle = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("Premium", circle.GetProperty("plan").GetString());
    }

    [Fact]
    public async Task An_invalid_purchase_is_402_and_the_plan_is_unchanged()
    {
        var response = await Subscribe(Sponsor, "forged-token");

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        Assert.Equal(PlanTier.Free, (await StateAsync()).Plan);
    }

    [Fact]
    public async Task A_purchase_cannot_pay_for_two_circles()
    {
        var other = await SeedPlanCircleAsync("elder-9", "sponsor-9");
        await Subscribe("sponsor-9", "good-token", other);

        var response = await Subscribe(Sponsor);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(PlanTier.Free, (await StateAsync()).Plan);
    }

    [Fact]
    public async Task Without_a_connected_store_subscribing_is_501()
    {
        Factory.Purchases.NotConnected = true;

        Assert.Equal(HttpStatusCode.NotImplemented, (await Subscribe(Sponsor)).StatusCode);
        Assert.Equal(PlanTier.Free, (await StateAsync()).Plan);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_missing_purchase_token_is_400(string? token)
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await Subscribe(Sponsor, token)).StatusCode);
    }

    [Fact]
    public async Task A_missing_body_and_a_too_long_token_are_400()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await Post("subscribe", Sponsor)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Subscribe(Sponsor, new string('x', 1025))).StatusCode);
    }

    [Theory]
    [InlineData("subscribe")]
    [InlineData("cancel")]
    public async Task Only_the_sponsor_changes_the_plan(string action)
    {
        await SeedUserAsync("stranger", AuthRole.Caregiver);
        var body = action == "subscribe" ? new { purchaseToken = "good-token" } : null;

        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(action, null, body)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(action, "stranger", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(action, Second, body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(action, Elder, body)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(action, Sponsor, body, Guid.NewGuid())).StatusCode);
        Assert.Equal(PlanTier.Free, (await StateAsync()).Plan);
    }

    // ---- Cancel / downgrade ----

    [Fact]
    public async Task Cancelling_a_circle_within_the_free_limit_removes_nobody()
    {
        using (var _ = Factory.CreateDbScope(out var db))
        {
            db.CircleMembers.Remove(await db.CircleMembers.SingleAsync(m => m.UserId == Third, Ct));
            await db.SaveChangesAsync(Ct);
        }
        await Subscribe(Sponsor);

        var response = await Post("cancel", Sponsor);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("Free", body.GetProperty("plan").GetString());
        Assert.Equal(0, body.GetProperty("removedMembers").GetArrayLength());
        var state = await StateAsync();
        Assert.Equal((PlanTier.Free, null), (state.Plan, state.Token));
        Assert.Equal([Second, Sponsor], state.Members);
    }

    [Fact]
    public async Task Downgrading_removes_the_most_recent_caregivers_above_the_free_limit()
    {
        await Subscribe(Sponsor);

        var response = await Post("cancel", Sponsor);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var removed = body.GetProperty("removedMembers");
        Assert.Equal(Third, Assert.Single(removed.EnumerateArray()).GetProperty("userId").GetString());
        Assert.Equal([Second, Sponsor], (await StateAsync()).Members);
    }

    [Fact]
    public async Task Downgrading_keeps_the_admin_and_the_sponsor_even_if_they_joined_last()
    {
        await Subscribe(Sponsor);
        using (var _ = Factory.CreateDbScope(out var db))
        {
            var sponsor = await db.CircleMembers.SingleAsync(m => m.UserId == Sponsor, Ct);
            db.CircleMembers.Remove(sponsor);
            db.CircleMembers.Add(new CircleMember
            {
                CircleId = _circle, UserId = Sponsor, Role = MemberRole.Admin, IsSponsor = true, JoinedAt = DateTimeOffset.UtcNow.AddDays(1),
            });
            await db.SaveChangesAsync(Ct);
        }

        await Post("cancel", Sponsor);

        Assert.Equal([Second, Sponsor], (await StateAsync()).Members);
    }

    [Fact]
    public async Task Cancelling_twice_is_harmless_and_does_not_remove_more()
    {
        await Subscribe(Sponsor);
        await Post("cancel", Sponsor);

        var response = await Post("cancel", Sponsor);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("removedMembers").GetArrayLength());
        Assert.Equal([Second, Sponsor], (await StateAsync()).Members);
    }

    [Fact]
    public async Task After_cancelling_the_purchase_can_pay_for_another_circle()
    {
        var other = await SeedPlanCircleAsync("elder-9", "sponsor-9");
        await Subscribe(Sponsor);
        await Post("cancel", Sponsor);

        Assert.Equal(HttpStatusCode.OK, (await Subscribe("sponsor-9", "good-token", other)).StatusCode);
    }
}
