using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Veiller.Api.Data.Entities;
using Veiller.Api.Tests.Infrastructure;
using AuthRole = Veiller.Api.Auth.Role;

namespace Veiller.Api.Tests.Health;

/// <summary>Shared helpers of the health sync tests: seeding, request building and JSON navigation.</summary>
public abstract class HealthApiTestBase : IDisposable
{
    protected const string Elder = "elder-1";
    protected const string Origin = "com.example.watchapp";

    /// <summary>A whole UTC hour yesterday: far enough from "now" for every record to be in the past.</summary>
    protected static readonly DateTimeOffset T0 = HourFloor(DateTimeOffset.UtcNow).AddDays(-1);

    protected readonly VeillerApiFactory Factory;
    protected readonly HttpClient Client;
    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    protected HealthApiTestBase(IReadOnlyDictionary<string, string?>? settings = null)
    {
        Factory = settings is null ? new VeillerApiFactory() : new VeillerApiFactory(settings);
        Client = Factory.CreateClient();
    }

    public void Dispose()
    {
        Client.Dispose();
        Factory.Dispose();
    }

    // ---- Seeding ----

    protected async Task SeedUserAsync(string uid, AuthRole role = AuthRole.Elder, bool inFirebase = false)
    {
        using var _ = Factory.CreateDbScope(out var db);
        if (await db.Users.AnyAsync(u => u.Id == uid, Ct)) return;
        db.Users.Add(new User { Id = uid, Email = $"{uid}@example.com", FullName = $"Name {uid}", Role = role, CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(Ct);
        if (inFirebase)
            Factory.Admin.Users[uid] = new Veiller.Api.Auth.FirebaseUser(uid, $"Name {uid}", $"{uid}@example.com", new Dictionary<string, object>());
    }

    // ---- Requests ----

    protected Task<HttpResponseMessage> SyncAsync(string? uid, object body) => SendRawAsync(HttpMethod.Post, "/api/health/sync", uid, Json(body));

    protected async Task<JsonElement> SyncOkAsync(string uid, object body)
    {
        var response = await SyncAsync(uid, body);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    protected Task<HttpResponseMessage> StatusAsync(string? uid) => SendRawAsync(HttpMethod.Get, "/api/health/me/status", uid, null);

    protected Task<HttpResponseMessage> SendRawAsync(
        HttpMethod method, string url, string? uid, string? json, string contentType = "application/json")
    {
        var request = new HttpRequestMessage(method, url);
        if (uid is not null) request.Headers.Add(TestAuthHandler.UidHeader, uid);
        if (json is not null) request.Content = new StringContent(json, Encoding.UTF8, contentType);
        return Client.SendAsync(request, Ct);
    }

    // ---- Request building ----

    protected static string Json(object value) => JsonSerializer.Serialize(value, JsonSerializerOptions.Web);

    /// <summary>A complete request with the given blocks; omitted blocks are absent from the JSON.</summary>
    protected static Dictionary<string, object?> Body(
        object[]? heartRate = null, object[]? steps = null, object[]? sleep = null, object? deleted = null, object? watchLastSeenAt = null)
    {
        var body = new Dictionary<string, object?> { ["schemaVersion"] = 1 };
        if (heartRate is not null) body["heartRateRecords"] = heartRate;
        if (steps is not null) body["stepsHourly"] = steps;
        if (sleep is not null) body["sleepSessions"] = sleep;
        if (deleted is not null) body["deleted"] = deleted;
        if (watchLastSeenAt is not null) body["watchLastSeenAt"] = watchLastSeenAt;
        return body;
    }

    protected static object Sample(DateTimeOffset time, long bpm) => new { time, bpm };

    /// <summary>A record from <paramref name="start"/> for ten minutes; by default one sample per minute at the given bpm.</summary>
    protected static object Record(string id, DateTimeOffset start, params object[] samples) =>
        new { hcRecordId = id, origin = Origin, start, end = start.AddMinutes(10), samples };

    protected static object Hour(DateTimeOffset start, long count) => new { start, end = start.AddHours(1), count };

    protected static object Session(string id, DateTimeOffset start, DateTimeOffset end, params object[] stages) =>
        new { hcRecordId = id, start, end, zoneOffset = "-05:00", origin = Origin, stages };

    protected static object Stage(DateTimeOffset start, DateTimeOffset end, string stage) => new { start, end, stage };

    /// <summary>A copy of <paramref name="item"/> with one member set (or removed when <paramref name="value"/> is null).</summary>
    protected static JsonObject With(object item, string member, object? value)
    {
        var node = JsonSerializer.SerializeToNode(item, JsonSerializerOptions.Web)!.AsObject();
        if (value is null) node.Remove(member);
        else node[member] = JsonSerializer.SerializeToNode(value, JsonSerializerOptions.Web);
        return node;
    }

    protected static DateTimeOffset HourFloor(DateTimeOffset value) =>
        new(value.Year, value.Month, value.Day, value.Hour, 0, 0, TimeSpan.Zero);

    // ---- Reading responses ----

    protected static JsonElement Rejected(JsonElement response, string block) => response.GetProperty("rejected").GetProperty(block);

    protected static int Accepted(JsonElement response, string counter) => response.GetProperty("accepted").GetProperty(counter).GetInt32();

    protected static string ReasonAt(JsonElement response, string block, int position) =>
        Rejected(response, block)[position].GetProperty("reason").GetString()!;

    protected async Task<JsonElement> ReadStatusAsync(string uid)
    {
        var response = await StatusAsync(uid);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    protected static DateTimeOffset? ReadInstant(JsonElement element, params string[] path)
    {
        foreach (var name in path) element = element.GetProperty(name);
        return element.ValueKind == JsonValueKind.Null ? null : element.GetDateTimeOffset();
    }

    // ---- Stored state ----

    protected async Task<List<(string RecordId, DateTime Time, int Bpm)>> StoredSamplesAsync(string elderId)
    {
        using var _ = Factory.CreateDbScope(out var db);
        var rows = await db.HeartRateSamples.Where(s => s.ElderId == elderId).ToListAsync(Ct);
        return rows.OrderBy(s => s.HcRecordId).ThenBy(s => s.Time).Select(s => (s.HcRecordId, s.Time, s.Bpm)).ToList();
    }

    protected async Task<int> StoredRowCountAsync(string elderId)
    {
        using var _ = Factory.CreateDbScope(out var db);
        return await db.HeartRateRecords.CountAsync(r => r.ElderId == elderId, Ct)
               + await db.HeartRateSamples.CountAsync(r => r.ElderId == elderId, Ct)
               + await db.StepsHourly.CountAsync(r => r.ElderId == elderId, Ct)
               + await db.SleepSessions.CountAsync(r => r.ElderId == elderId, Ct)
               + await db.SleepStages.CountAsync(r => r.ElderId == elderId, Ct);
    }
}
