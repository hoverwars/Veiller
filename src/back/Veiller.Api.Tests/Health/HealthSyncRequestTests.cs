using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AuthRole = Veiller.Api.Auth.Role;

namespace Veiller.Api.Tests.Health;

/// <summary>Whole-request behavior of <c>POST /api/health/sync</c>: who may call it, what makes it fail, which limits apply.</summary>
public sealed class HealthSyncRequestTests : HealthApiTestBase
{
    // ---- Authentication and role ----

    [Fact]
    public async Task Sync_without_token_is_401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await SyncAsync(null, Body())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await StatusAsync(null)).StatusCode);
    }

    [Fact]
    public async Task Caregiver_is_403_on_sync_and_status()
    {
        await SeedUserAsync("cg", AuthRole.Caregiver);

        var sync = await SyncAsync("cg", Body());
        Assert.Equal(HttpStatusCode.Forbidden, sync.StatusCode);
        Assert.Equal("application/problem+json", sync.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.Forbidden, (await StatusAsync("cg")).StatusCode);
    }

    [Fact]
    public async Task Authenticated_user_without_profile_is_403()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await SyncAsync("ghost", Body())).StatusCode);
    }

    [Fact]
    public async Task Caregiver_cannot_write_even_with_an_elder_id_in_the_body()
    {
        await SeedUserAsync(Elder);
        await SeedUserAsync("cg", AuthRole.Caregiver);
        var body = Body(heartRate: [Record("hr-1", T0, Sample(T0.AddMinutes(1), 70))]);
        body["elderId"] = Elder;

        Assert.Equal(HttpStatusCode.Forbidden, (await SyncAsync("cg", body)).StatusCode);
        Assert.Equal(0, await StoredRowCountAsync(Elder));
    }

    [Fact]
    public async Task Owner_is_the_caller_whatever_the_body_says()
    {
        await SeedUserAsync(Elder);
        await SeedUserAsync("elder-2");
        var body = Body(heartRate: [Record("hr-1", T0, Sample(T0.AddMinutes(1), 70))]);
        body["elderId"] = "elder-2";
        body["userId"] = "elder-2";

        await SyncOkAsync(Elder, body);

        Assert.Single(await StoredSamplesAsync(Elder));
        Assert.Empty(await StoredSamplesAsync("elder-2"));
    }

    // ---- Valid requests ----

    [Fact]
    public async Task Request_with_only_schema_version_is_200_with_zero_counts()
    {
        await SeedUserAsync(Elder);

        var response = await SyncOkAsync(Elder, Body());

        Assert.Equal(0, Accepted(response, "heartRateSamples"));
        Assert.Equal(0, Accepted(response, "stepsHourly"));
        Assert.Equal(0, Accepted(response, "sleepSessions"));
        Assert.Equal(0, Accepted(response, "deleted"));
        foreach (var block in new[] { "heartRateRecords", "stepsHourly", "sleepSessions", "deleted" })
            Assert.Equal(0, Rejected(response, block).GetArrayLength());
        Assert.Equal(JsonValueKind.Null, Rejected(response, "watchLastSeenAt").ValueKind);
        Assert.InRange((DateTimeOffset.UtcNow - ReadInstant(response, "serverTime")!.Value).Duration(), TimeSpan.Zero, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Empty_blocks_and_null_blocks_are_valid()
    {
        await SeedUserAsync(Elder);
        var body = Body(heartRate: [], steps: [], sleep: [], deleted: new { heartRateRecords = Array.Empty<string>(), sleepSessions = (string[]?)null });
        body["client"] = null;

        Assert.Equal(HttpStatusCode.OK, (await SyncAsync(Elder, body)).StatusCode);
    }

    [Fact]
    public async Task Unknown_fields_are_ignored()
    {
        await SeedUserAsync(Elder);
        var body = Body(heartRate: [With(Record("hr-1", T0, Sample(T0.AddMinutes(1), 70)), "futureField", 42)]);
        body["somethingNew"] = new { a = 1 };
        body["client"] = new { appVersion = "0.1.0", deviceManufacturer = "samsung", deviceModel = "SM-S931B", extra = true };

        var response = await SyncOkAsync(Elder, body);

        Assert.Equal(1, Accepted(response, "heartRateSamples"));
    }

    // ---- 400 ----

    [Theory]
    [InlineData("""{"heartRateRecords":[]}""")]                       // schemaVersion missing
    [InlineData("""{"schemaVersion":null}""")]
    [InlineData("""{"schemaVersion":2}""")]
    [InlineData("""{"schemaVersion":0}""")]
    [InlineData("""{"schemaVersion":-1}""")]
    public async Task Unsupported_or_missing_schema_version_is_400(string json)
    {
        await SeedUserAsync(Elder);

        var response = await SendRawAsync(HttpMethod.Post, "/api/health/sync", Elder, json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("""{"schemaVersion":1""")]                            // truncated
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData(""""{"schemaVersion":"1"}"""")]                       // number as string
    [InlineData("""{"schemaVersion":1,"heartRateRecords":{}}""")]     // object instead of array
    [InlineData("""{"schemaVersion":1,"heartRateRecords":"x"}""")]
    [InlineData("""{"schemaVersion":1,"stepsHourly":[{"start":"2026-11-01T14:00:00Z","end":"2026-11-01T15:00:00Z","count":"12"}]}""")]
    [InlineData("""{"schemaVersion":1,"stepsHourly":[{"start":"2026-11-01T14:00:00Z","end":"2026-11-01T15:00:00Z","count":1.5}]}""")]
    [InlineData("""{"schemaVersion":1,"stepsHourly":[{"start":"yesterday","end":"2026-11-01T15:00:00Z","count":1}]}""")]
    [InlineData("""{"schemaVersion":1,"heartRateRecords":[{"hcRecordId":5,"origin":"x"}]}""")]
    [InlineData("""{"schemaVersion":1,"heartRateRecords":[{"hcRecordId":"a","samples":[{"time":"2026-11-01T14:00:00Z","bpm":72.5}]}]}""")]
    [InlineData("""{"schemaVersion":1,"watchLastSeenAt":"soon"}""")]
    [InlineData("""{"schemaVersion":1,"deleted":{"heartRateRecords":"abc"}}""")]
    public async Task Malformed_json_or_wrong_types_is_400_and_stores_nothing(string json)
    {
        await SeedUserAsync(Elder);

        var response = await SendRawAsync(HttpMethod.Post, "/api/health/sync", Elder, json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await StoredRowCountAsync(Elder));
        Assert.Equal(JsonValueKind.Null, (await ReadStatusAsync(Elder)).GetProperty("lastUploadAt").ValueKind);
    }

    [Fact]
    public async Task Non_json_content_type_is_415()
    {
        await SeedUserAsync(Elder);

        var response = await SendRawAsync(HttpMethod.Post, "/api/health/sync", Elder, """{"schemaVersion":1}""", "text/plain");

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task Bad_request_does_not_echo_the_submitted_values()
    {
        await SeedUserAsync(Elder);

        var response = await SendRawAsync(
            HttpMethod.Post, "/api/health/sync", Elder, """{"schemaVersion":1,"heartRateRecords":[{"hcRecordId":"SECRET-ID","origin":{"x":1}}]}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("SECRET-ID", await response.Content.ReadAsStringAsync(Ct));
    }
}

/// <summary>The limits are server configuration: these tests run the API with tiny ones.</summary>
public sealed class HealthSyncLimitTests() : HealthApiTestBase(new Dictionary<string, string?>
{
    ["HealthSync:MaxItemsPerRequest"] = "5",
    ["HealthSync:MaxBodyBytes"] = "4000",
    ["HealthSync:FutureTolerance"] = "00:00:01",
})
{
    [Fact]
    public async Task Exactly_the_item_limit_is_accepted()
    {
        await SeedUserAsync(Elder);
        var samples = Enumerable.Range(1, 3).Select(i => Sample(T0.AddMinutes(i), 70)).ToArray();
        var body = Body(heartRate: [Record("hr-1", T0, samples)], steps: [Hour(T0, 10)], deleted: new { heartRateRecords = new[] { "gone" } });

        var response = await SyncOkAsync(Elder, body);

        Assert.Equal(3, Accepted(response, "heartRateSamples"));
    }

    [Fact]
    public async Task More_items_than_the_limit_is_413_and_nothing_is_stored()
    {
        await SeedUserAsync(Elder);
        var samples = Enumerable.Range(1, 3).Select(i => Sample(T0.AddMinutes(i), 70)).ToArray();
        // 3 samples + 1 bucket + 1 session + 1 deleted id = 6 > 5.
        var body = Body(
            heartRate: [Record("hr-1", T0, samples)], steps: [Hour(T0, 10)],
            sleep: [Session("s-1", T0, T0.AddHours(7))], deleted: new { heartRateRecords = new[] { "gone" } });

        var response = await SyncAsync(Elder, body);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(0, await StoredRowCountAsync(Elder));
        Assert.Equal(JsonValueKind.Null, (await ReadStatusAsync(Elder)).GetProperty("lastUploadAt").ValueKind);
    }

    [Fact]
    public async Task Items_are_counted_even_when_they_are_invalid()
    {
        await SeedUserAsync(Elder);
        var bad = Enumerable.Range(0, 6).Select(_ => With(Hour(T0, 1), "count", -1)).Cast<object>().ToArray();

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await SyncAsync(Elder, Body(steps: bad))).StatusCode);
    }

    [Fact]
    public async Task Stages_and_records_do_not_count_as_items()
    {
        await SeedUserAsync(Elder);
        var stages = Enumerable.Range(0, 6).Select(i => Stage(T0.AddMinutes(i * 10), T0.AddMinutes(i * 10 + 10), "LIGHT")).ToArray();
        var body = Body(
            heartRate: [Record("a", T0), Record("b", T0), Record("c", T0), Record("d", T0), Record("e", T0), Record("f", T0)],
            sleep: [Session("s-1", T0, T0.AddHours(2), stages)]);

        Assert.Equal(HttpStatusCode.OK, (await SyncAsync(Elder, body)).StatusCode);
    }

    [Fact]
    public async Task Body_over_the_byte_limit_is_413_and_nothing_is_stored()
    {
        await SeedUserAsync(Elder);
        // A handful of items (under the item limit) but a long origin string pushes the body past 4000 bytes.
        var record = With(Record("hr-1", T0, Sample(T0.AddMinutes(1), 70)), "origin", new string('o', 6000));

        var response = await SyncAsync(Elder, Body(heartRate: [record]));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal(0, await StoredRowCountAsync(Elder));
    }

    [Fact]
    public async Task Chunked_body_over_the_byte_limit_is_413()
    {
        await SeedUserAsync(Elder);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/health/sync")
        {
            Content = new StreamContent(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(Json(Body(sleep: [Session("s", T0, T0.AddHours(1), Stage(T0, T0.AddMinutes(5), new string('x', 6000)))])))))
            {
                Headers = { ContentType = new("application/json") },
            },
        };
        request.Headers.TransferEncodingChunked = true;
        request.Headers.Add(Infrastructure.TestAuthHandler.UidHeader, Elder);

        var response = await Client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task Future_tolerance_comes_from_configuration()
    {
        await SeedUserAsync(Elder);
        // Tolerance is 1 s here: a sample 30 s ahead is rejected (it would pass with the default 5 min).
        var now = DateTimeOffset.UtcNow;
        var record = new { hcRecordId = "hr-1", origin = Origin, start = now.AddMinutes(-5), end = now.AddMinutes(5), samples = new[] { Sample(now.AddSeconds(30), 70) } };

        var response = await SyncOkAsync(Elder, Body(heartRate: [record]));

        Assert.Equal("TIME_IN_FUTURE", ReasonAt(response, "heartRateRecords", 0));
    }
}
