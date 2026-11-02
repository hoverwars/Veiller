using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Veiller.Api.Data.Entities;
using Veiller.Api.Tests.Patients;
using AuthRole = Veiller.Api.Auth.Role;

namespace Veiller.Api.Tests.Alerts;

public abstract class AlertApiTestBase : PatientApiTestBase
{
    protected const string OtherElder = "elder-2";

    protected AlertApiTestBase(IReadOnlyDictionary<string, string?>? settings = null) : base(settings) { }

    /// <summary>A heart rate record ending now, so its last sample is "current" for the evaluator.</summary>
    protected Task SyncRecentBpmAsync(string elderId, string recordId, long bpm)
    {
        var start = DateTimeOffset.UtcNow.AddMinutes(-3);
        return SyncHeartRateAsync(elderId, recordId, start, bpm);
    }

    protected Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string url, string? uid, object? body) =>
        SendRawAsync(method, url, uid, body is null ? null : Json(body));

    protected async Task<JsonElement> AlertsOfAsync(Guid circleId, string uid, string query = "")
    {
        var response = await GetAsync($"/api/circles/{circleId}/alerts{query}", uid);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    protected async Task<List<Alert>> StoredAlertsAsync(Guid circleId)
    {
        using var _ = Factory.CreateDbScope(out var db);
        return await db.Alerts.AsNoTracking().Where(a => a.CircleId == circleId).ToListAsync(Ct);
    }

    protected async Task AddDeviceTokenAsync(string uid, string token)
    {
        using var _ = Factory.CreateDbScope(out var db);
        db.DeviceTokens.Add(new DeviceToken { Token = token, UserId = uid, Platform = "android", UpdatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(Ct);
    }

    protected async Task SetRuleAsync(Guid circleId, AlertType type, bool enabled, int threshold)
    {
        using var _ = Factory.CreateDbScope(out var db);
        db.AlertRules.Add(new AlertRule { CircleId = circleId, Type = type, Enabled = enabled, Threshold = threshold });
        await db.SaveChangesAsync(Ct);
    }

    protected async Task<Guid> SeedAdminCircleAsync(string elderId, string admin, params string[] caregivers)
    {
        var id = await SeedCircleAsync(elderId, [admin, .. caregivers]);
        using var _ = Factory.CreateDbScope(out var db);
        (await db.CircleMembers.SingleAsync(m => m.CircleId == id && m.UserId == admin, Ct)).Role = MemberRole.Admin;
        await db.SaveChangesAsync(Ct);
        return id;
    }

    protected static string FirstId(JsonElement alerts) => alerts[0].GetProperty("id").GetString()!;
}
