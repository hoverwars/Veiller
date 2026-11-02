using System.Net;

namespace Veiller.Api.Tests.Health;

/// <summary>Limits that keep one bad record or one noisy client from blocking or flooding the sync.</summary>
public sealed class HealthSyncRecordSizeTests() : HealthApiTestBase(new Dictionary<string, string?>
{
    ["HealthSync:MaxItemsPerRequest"] = "10",
    ["HealthSync:MaxSamplesPerRecord"] = "4",
})
{
    private static object[] Samples(int count) => Enumerable.Range(0, count).Select(i => Sample(T0.AddSeconds(i * 10), 70)).ToArray();

    [Fact]
    public async Task Record_over_the_sample_limit_is_rejected_as_an_item_not_as_a_413()
    {
        await SeedUserAsync(Elder);
        var tooBig = Record("workout", T0, Samples(5));

        var response = await SyncOkAsync(Elder, Body(heartRate: [tooBig, Record("ok", T0, Samples(2))]));

        Assert.Equal("TOO_MANY_SAMPLES", ReasonAt(response, "heartRateRecords", 0));
        Assert.Equal(0, Rejected(response, "heartRateRecords")[0].GetProperty("index").GetInt32());
        Assert.Equal(2, Accepted(response, "heartRateSamples"));
        Assert.Equal(["ok"], (await StoredSamplesAsync(Elder)).Select(s => s.RecordId).Distinct());
    }

    [Fact]
    public async Task Oversized_record_does_not_count_toward_the_request_item_limit()
    {
        await SeedUserAsync(Elder);

        // 5 samples (rejected) + 10 valid ones would be 15 > 10 if the rejected record counted.
        var response = await SyncOkAsync(Elder, Body(heartRate: [Record("big", T0, Samples(5)), Record("a", T0, Samples(4)), Record("b", T0, Samples(4))]));

        Assert.Equal(8, Accepted(response, "heartRateSamples"));
    }

    [Fact]
    public async Task Rejected_oversized_record_leaves_the_stored_samples_untouched()
    {
        await SeedUserAsync(Elder);
        await SyncOkAsync(Elder, Body(heartRate: [Record("workout", T0, Samples(3))]));

        await SyncOkAsync(Elder, Body(heartRate: [Record("workout", T0, Samples(5))]));

        Assert.Equal(3, (await StoredSamplesAsync(Elder)).Count);
    }

    [Fact]
    public async Task Record_with_exactly_the_sample_limit_is_accepted()
    {
        await SeedUserAsync(Elder);

        var response = await SyncOkAsync(Elder, Body(heartRate: [Record("edge", T0, Samples(4))]));

        Assert.Equal(4, Accepted(response, "heartRateSamples"));
    }
}

public sealed class HealthSyncAgeTests : HealthApiTestBase
{
    public HealthSyncAgeTests() => SeedUserAsync(Elder).GetAwaiter().GetResult();

    private static readonly DateTimeOffset Old = HourFloor(DateTimeOffset.UtcNow).AddDays(-40);

    [Fact]
    public async Task Heart_rate_record_older_than_the_limit_is_rejected()
    {
        var response = await SyncOkAsync(Elder, Body(heartRate: [Record("old", Old, Sample(Old.AddMinutes(1), 70))]));

        Assert.Equal("TIME_TOO_OLD", ReasonAt(response, "heartRateRecords", 0));
        Assert.Equal(0, Accepted(response, "heartRateSamples"));
    }

    [Fact]
    public async Task Steps_bucket_older_than_the_limit_is_rejected()
    {
        var response = await SyncOkAsync(Elder, Body(steps: [Hour(Old, 100), Hour(T0, 50)]));

        Assert.Equal("TIME_TOO_OLD", ReasonAt(response, "stepsHourly", 0));
        Assert.Equal(1, Accepted(response, "stepsHourly"));
    }

    [Fact]
    public async Task Sleep_session_older_than_the_limit_is_rejected()
    {
        var response = await SyncOkAsync(Elder, Body(sleep: [Session("s-old", Old, Old.AddHours(7))]));

        Assert.Equal("TIME_TOO_OLD", ReasonAt(response, "sleepSessions", 0));
        Assert.Equal(0, Accepted(response, "sleepSessions"));
    }

    [Fact]
    public async Task Data_within_the_last_thirty_days_is_still_accepted()
    {
        var recent = HourFloor(DateTimeOffset.UtcNow).AddDays(-30);

        var response = await SyncOkAsync(Elder, Body(steps: [Hour(recent, 10)]));

        Assert.Equal(1, Accepted(response, "stepsHourly"));
    }
}

public sealed class HealthSyncRateLimitTests() : HealthApiTestBase(new Dictionary<string, string?>
{
    ["RateLimiting:HealthSyncPerMinute"] = "2",
})
{
    [Fact]
    public async Task Third_sync_within_the_window_is_429()
    {
        await SeedUserAsync(Elder);

        Assert.Equal(HttpStatusCode.OK, (await SyncAsync(Elder, Body())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SyncAsync(Elder, Body())).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await SyncAsync(Elder, Body())).StatusCode);
    }

    [Fact]
    public async Task The_limit_is_per_elder()
    {
        await SeedUserAsync(Elder);
        await SeedUserAsync("elder-2");
        for (var i = 0; i < 2; i++) await SyncAsync(Elder, Body());

        Assert.Equal(HttpStatusCode.OK, (await SyncAsync("elder-2", Body())).StatusCode);
    }

    [Fact]
    public async Task Status_endpoint_is_not_rate_limited()
    {
        await SeedUserAsync(Elder);
        for (var i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.OK, (await StatusAsync(Elder)).StatusCode);
    }
}
