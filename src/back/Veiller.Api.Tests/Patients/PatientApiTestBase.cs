using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Veiller.Api.Consents;
using Veiller.Api.Data.Entities;
using Veiller.Api.Tests.Health;
using AuthRole = Veiller.Api.Auth.Role;

namespace Veiller.Api.Tests.Patients;

/// <summary>Seeding and request helpers of the patient tests; health data goes in through the real sync endpoint.</summary>
public abstract class PatientApiTestBase : HealthApiTestBase
{
    protected const string Caregiver = "caregiver-1";

    protected static DateTimeOffset Now => DateTimeOffset.UtcNow;

    /// <summary>An elder owning a circle, with the given caregivers (role Caregiver).</summary>
    protected async Task<Guid> SeedCircleAsync(string elderId, params string[] caregivers)
    {
        await SeedUserAsync(elderId);
        foreach (var uid in caregivers) await SeedUserAsync(uid, AuthRole.Caregiver);

        using var _ = Factory.CreateDbScope(out var db);
        var now = DateTimeOffset.UtcNow;
        var circle = new Circle { OwnerUserId = elderId, CreatedAt = now };
        circle.Members.Add(new CircleMember { CircleId = circle.Id, UserId = elderId, Role = MemberRole.DataOwner, JoinedAt = now });
        foreach (var uid in caregivers)
            circle.Members.Add(new CircleMember { CircleId = circle.Id, UserId = uid, Role = MemberRole.Caregiver, JoinedAt = now.AddMinutes(1) });
        db.Circles.Add(circle);
        await db.SaveChangesAsync(Ct);
        return circle.Id;
    }

    protected async Task RevokeConsentAsync(string ownerId, HealthDataType type)
    {
        using var _ = Factory.CreateDbScope(out var db);
        db.Consents.Add(new Consent { UserId = ownerId, Type = type, Granted = false, UpdatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(Ct);
    }

    protected Task<HttpResponseMessage> GetAsync(string url, string? uid) => SendRawAsync(HttpMethod.Get, url, uid, null);

    protected async Task<JsonElement> GetOkAsync(string url, string uid)
    {
        var response = await GetAsync(url, uid);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    /// <summary>One heart rate record with one sample per minute from <paramref name="start"/>, at the given bpm values.</summary>
    protected async Task SyncHeartRateAsync(string elderId, string recordId, DateTimeOffset start, params long[] bpms)
    {
        var samples = bpms.Select((bpm, i) => Sample(start.AddMinutes(i), bpm)).ToArray();
        await SyncOkAsync(elderId, Body(heartRate: [Record(recordId, start, samples)]));
    }

    protected static bool IsNull(JsonElement element, string member) => element.GetProperty(member).ValueKind == JsonValueKind.Null;

    protected static bool HasValue(JsonElement point) => point.GetProperty("value").ValueKind == JsonValueKind.Number;
}
