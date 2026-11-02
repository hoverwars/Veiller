using System.Text.Json;

namespace Veiller.Api.Tests.Health;

/// <summary>
/// A bad item never fails the request: it is skipped and reported by position and reason code (never by value),
/// and the response stays 200 so the client can move its token forward.
/// </summary>
public sealed class HealthSyncRejectionTests : HealthApiTestBase
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    public HealthSyncRejectionTests() => SeedUserAsync(Elder).GetAwaiter().GetResult();

    private static object ValidRecord(string id = "hr-1") => Record(id, T0, Sample(T0.AddMinutes(1), 72));

    // ---- Heart rate: the record is rejected whole ----

    [Theory]
    [InlineData("hcRecordId", null, "MISSING_FIELD")]
    [InlineData("hcRecordId", "", "MISSING_FIELD")]
    [InlineData("hcRecordId", "   ", "MISSING_FIELD")]
    [InlineData("origin", null, "MISSING_FIELD")]
    [InlineData("origin", "", "MISSING_FIELD")]
    [InlineData("start", null, "MISSING_FIELD")]
    [InlineData("end", null, "MISSING_FIELD")]
    [InlineData("samples", null, "MISSING_FIELD")]
    [InlineData("hcRecordId", "LONG129", "FIELD_TOO_LONG")]
    [InlineData("origin", "LONG256", "FIELD_TOO_LONG")]
    public async Task Heart_rate_record_with_a_bad_field_is_rejected_whole(string member, string? value, string reason)
    {
        var bad = With(ValidRecord("bad"), member, Expand(value));

        var response = await SyncOkAsync(Elder, Body(heartRate: [ValidRecord("ok"), bad]));

        Assert.Equal(1, Rejected(response, "heartRateRecords").GetArrayLength());
        var rejection = Rejected(response, "heartRateRecords")[0];
        Assert.Equal(1, rejection.GetProperty("index").GetInt32());
        Assert.Equal(reason, rejection.GetProperty("reason").GetString());
        Assert.False(rejection.TryGetProperty("sampleIndex", out _));
        Assert.Equal(1, Accepted(response, "heartRateSamples"));
        Assert.Equal(["ok"], (await StoredSamplesAsync(Elder)).Select(s => s.RecordId).Distinct());
    }

    [Fact]
    public async Task Heart_rate_record_with_end_before_start_is_invalid_interval()
    {
        var bad = With(ValidRecord(), "end", T0.AddMinutes(-1));

        var response = await SyncOkAsync(Elder, Body(heartRate: [bad]));

        Assert.Equal("INVALID_INTERVAL", ReasonAt(response, "heartRateRecords", 0));
    }

    [Fact]
    public async Task Heart_rate_record_starting_in_the_future_is_rejected()
    {
        var bad = Record("future", Now.AddHours(1), Sample(Now.AddHours(1), 70));

        var response = await SyncOkAsync(Elder, Body(heartRate: [bad]));

        Assert.Equal("TIME_IN_FUTURE", ReasonAt(response, "heartRateRecords", 0));
    }

    [Fact]
    public async Task Null_item_in_a_block_is_a_missing_field_at_its_position()
    {
        var body = Body();
        body["heartRateRecords"] = new object?[] { ValidRecord(), null };
        body["stepsHourly"] = new object?[] { null };
        body["sleepSessions"] = new object?[] { null };

        var response = await SyncOkAsync(Elder, body);

        Assert.Equal("MISSING_FIELD", ReasonAt(response, "heartRateRecords", 0));
        Assert.Equal(1, Rejected(response, "heartRateRecords")[0].GetProperty("index").GetInt32());
        Assert.Equal("MISSING_FIELD", ReasonAt(response, "stepsHourly", 0));
        Assert.Equal("MISSING_FIELD", ReasonAt(response, "sleepSessions", 0));
    }

    [Fact]
    public async Task Rejected_record_leaves_the_stored_samples_untouched()
    {
        await SyncOkAsync(Elder, Body(heartRate: [Record("hr-1", T0, Sample(T0.AddMinutes(1), 70), Sample(T0.AddMinutes(2), 71))]));

        // Same record id, but the interval is invalid: nothing of it may replace the stored samples.
        var bad = With(Record("hr-1", T0, Sample(T0.AddMinutes(3), 99)), "end", T0.AddMinutes(-5));
        var response = await SyncOkAsync(Elder, Body(heartRate: [bad]));

        Assert.Equal("INVALID_INTERVAL", ReasonAt(response, "heartRateRecords", 0));
        Assert.Equal([70, 71], (await StoredSamplesAsync(Elder)).Select(s => s.Bpm));
    }

    // ---- Heart rate: only the sample is dropped ----

    [Theory]
    [InlineData(0, "BPM_OUT_OF_RANGE")]
    [InlineData(-5, "BPM_OUT_OF_RANGE")]
    [InlineData(301, "BPM_OUT_OF_RANGE")]
    [InlineData(5_000_000_000, "BPM_OUT_OF_RANGE")]   // beyond int: still a per-item rejection, not a 400
    public async Task Sample_with_a_bad_bpm_is_dropped_alone(long bpm, string reason)
    {
        var record = Record("hr-1", T0, Sample(T0.AddMinutes(1), 70), Sample(T0.AddMinutes(2), bpm), Sample(T0.AddMinutes(3), 72));

        var response = await SyncOkAsync(Elder, Body(heartRate: [record]));

        var rejection = Rejected(response, "heartRateRecords")[0];
        Assert.Equal(0, rejection.GetProperty("index").GetInt32());
        Assert.Equal(1, rejection.GetProperty("sampleIndex").GetInt32());
        Assert.Equal(reason, rejection.GetProperty("reason").GetString());
        Assert.Equal(2, Accepted(response, "heartRateSamples"));
        Assert.Equal([70, 72], (await StoredSamplesAsync(Elder)).Select(s => s.Bpm));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(300)]
    public async Task Bpm_bounds_are_inclusive(long bpm)
    {
        var response = await SyncOkAsync(Elder, Body(heartRate: [Record("hr-1", T0, Sample(T0.AddMinutes(1), bpm))]));

        Assert.Equal(1, Accepted(response, "heartRateSamples"));
    }

    [Fact]
    public async Task Sample_in_the_future_is_dropped_but_the_record_end_may_be_in_the_future()
    {
        // Watch apps write blocks whose end is still ahead: the record is kept, only the future sample goes.
        var start = Now.AddMinutes(-10);
        var record = new { hcRecordId = "hr-1", origin = Origin, start, end = Now.AddMinutes(20), samples = new[] { Sample(Now.AddMinutes(-5), 70), Sample(Now.AddMinutes(15), 71) } };

        var response = await SyncOkAsync(Elder, Body(heartRate: [record]));

        var rejection = Rejected(response, "heartRateRecords")[0];
        Assert.Equal(1, rejection.GetProperty("sampleIndex").GetInt32());
        Assert.Equal("TIME_IN_FUTURE", rejection.GetProperty("reason").GetString());
        Assert.Equal(1, Accepted(response, "heartRateSamples"));
    }

    [Fact]
    public async Task Sample_within_the_tolerance_of_server_time_is_accepted()
    {
        var record = new { hcRecordId = "hr-1", origin = Origin, start = Now.AddMinutes(-5), end = Now.AddMinutes(5), samples = new[] { Sample(Now.AddMinutes(2), 70) } };

        var response = await SyncOkAsync(Elder, Body(heartRate: [record]));

        Assert.Equal(1, Accepted(response, "heartRateSamples"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(601)]
    public async Task Sample_outside_its_record_is_dropped(int secondsFromStart)
    {
        var record = Record("hr-1", T0, Sample(T0.AddSeconds(secondsFromStart), 70), Sample(T0.AddMinutes(5), 71));

        var response = await SyncOkAsync(Elder, Body(heartRate: [record]));

        Assert.Equal("OUTSIDE_PARENT", ReasonAt(response, "heartRateRecords", 0));
        Assert.Equal(1, Accepted(response, "heartRateSamples"));
    }

    [Fact]
    public async Task Sample_on_the_record_bounds_is_accepted()
    {
        var record = Record("hr-1", T0, Sample(T0, 70), Sample(T0.AddMinutes(10), 71));

        Assert.Equal(2, Accepted(await SyncOkAsync(Elder, Body(heartRate: [record])), "heartRateSamples"));
    }

    [Theory]
    [InlineData("time")]
    [InlineData("bpm")]
    public async Task Sample_with_a_missing_field_is_dropped(string member)
    {
        var record = Record("hr-1", T0, With(Sample(T0.AddMinutes(1), 70), member, null), Sample(T0.AddMinutes(2), 71));

        var response = await SyncOkAsync(Elder, Body(heartRate: [record]));

        Assert.Equal("MISSING_FIELD", ReasonAt(response, "heartRateRecords", 0));
        Assert.Equal(0, Rejected(response, "heartRateRecords")[0].GetProperty("sampleIndex").GetInt32());
        Assert.Equal(1, Accepted(response, "heartRateSamples"));
    }

    // ---- Steps ----

    [Theory]
    [InlineData(30, 60, "BUCKET_NOT_ALIGNED")]    // starts at :30
    [InlineData(0, 30, "BUCKET_NOT_ALIGNED")]     // half an hour
    [InlineData(0, 120, "BUCKET_NOT_ALIGNED")]    // two hours
    [InlineData(0, 0, "INVALID_INTERVAL")]        // end == start
    [InlineData(0, -60, "INVALID_INTERVAL")]      // end before start
    public async Task Steps_bucket_must_be_exactly_one_whole_utc_hour(int startMinutes, int lengthMinutes, string reason)
    {
        var start = T0.AddMinutes(startMinutes);
        var bucket = new { start, end = start.AddMinutes(lengthMinutes), count = 10 };

        var response = await SyncOkAsync(Elder, Body(steps: [Hour(T0.AddHours(-1), 5), bucket]));

        Assert.Equal(1, Rejected(response, "stepsHourly")[0].GetProperty("index").GetInt32());
        Assert.Equal(reason, ReasonAt(response, "stepsHourly", 0));
        Assert.Equal(1, Accepted(response, "stepsHourly"));
    }

    [Fact]
    public async Task Steps_bucket_with_seconds_is_not_aligned()
    {
        var bucket = new { start = T0.AddSeconds(1), end = T0.AddSeconds(1).AddHours(1), count = 10 };

        Assert.Equal("BUCKET_NOT_ALIGNED", ReasonAt(await SyncOkAsync(Elder, Body(steps: [bucket])), "stepsHourly", 0));
    }

    [Fact]
    public async Task Steps_bucket_of_the_current_hour_is_accepted_although_it_ends_in_the_future()
    {
        var response = await SyncOkAsync(Elder, Body(steps: [Hour(HourFloor(Now), 120)]));

        Assert.Equal(1, Accepted(response, "stepsHourly"));
        Assert.Equal(0, Rejected(response, "stepsHourly").GetArrayLength());
    }

    [Fact]
    public async Task Steps_bucket_starting_in_the_future_is_rejected()
    {
        var response = await SyncOkAsync(Elder, Body(steps: [Hour(HourFloor(Now).AddHours(2), 5)]));

        Assert.Equal("TIME_IN_FUTURE", ReasonAt(response, "stepsHourly", 0));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1_000_001)]
    [InlineData(9_999_999_999)]
    public async Task Steps_count_out_of_range_is_rejected(long count)
    {
        var response = await SyncOkAsync(Elder, Body(steps: [Hour(T0, count)]));

        Assert.Equal("COUNT_OUT_OF_RANGE", ReasonAt(response, "stepsHourly", 0));
    }

    [Theory]
    [InlineData("start")]
    [InlineData("end")]
    [InlineData("count")]
    public async Task Steps_bucket_with_a_missing_field_is_rejected(string member)
    {
        var response = await SyncOkAsync(Elder, Body(steps: [With(Hour(T0, 5), member, null)]));

        Assert.Equal("MISSING_FIELD", ReasonAt(response, "stepsHourly", 0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1_000_000)]
    public async Task Steps_count_bounds_are_inclusive(long count)
    {
        Assert.Equal(1, Accepted(await SyncOkAsync(Elder, Body(steps: [Hour(T0, count)])), "stepsHourly"));
    }

    // ---- Sleep ----

    [Theory]
    [InlineData("hcRecordId", null, "MISSING_FIELD")]
    [InlineData("origin", null, "MISSING_FIELD")]
    [InlineData("start", null, "MISSING_FIELD")]
    [InlineData("end", null, "MISSING_FIELD")]
    [InlineData("hcRecordId", "LONG129", "FIELD_TOO_LONG")]
    [InlineData("origin", "LONG256", "FIELD_TOO_LONG")]
    [InlineData("zoneOffset", "UTC", "INVALID_FORMAT")]
    [InlineData("zoneOffset", "0500", "INVALID_FORMAT")]
    [InlineData("zoneOffset", "+5:00", "INVALID_FORMAT")]
    [InlineData("zoneOffset", "+19:00", "INVALID_FORMAT")]
    [InlineData("zoneOffset", "-05:60", "INVALID_FORMAT")]
    [InlineData("zoneOffset", "+٠٥:00", "INVALID_FORMAT")]   // Arabic-Indic digits: int.Parse would throw (500)
    public async Task Sleep_session_with_a_bad_field_is_rejected(string member, string? value, string reason)
    {
        var good = Session("ok", T0, T0.AddHours(7));
        var bad = With(Session("bad", T0, T0.AddHours(7)), member, Expand(value));

        var response = await SyncOkAsync(Elder, Body(sleep: [good, bad]));

        Assert.Equal(1, Rejected(response, "sleepSessions")[0].GetProperty("index").GetInt32());
        Assert.Equal(reason, ReasonAt(response, "sleepSessions", 0));
        Assert.Equal(1, Accepted(response, "sleepSessions"));
    }

    [Theory]
    [InlineData("-05:00")]
    [InlineData("+00:00")]
    [InlineData("+05:30")]
    [InlineData("+18:00")]
    [InlineData("-18:00")]
    public async Task Valid_zone_offsets_are_accepted(string offset)
    {
        var session = With(Session("s-1", T0, T0.AddHours(7)), "zoneOffset", offset);

        Assert.Equal(1, Accepted(await SyncOkAsync(Elder, Body(sleep: [session])), "sleepSessions"));
    }

    [Fact]
    public async Task Sleep_session_without_zone_offset_is_accepted()
    {
        // Health Connect itself allows a session without an offset.
        var session = With(Session("s-1", T0, T0.AddHours(7)), "zoneOffset", null);

        Assert.Equal(1, Accepted(await SyncOkAsync(Elder, Body(sleep: [session])), "sleepSessions"));
    }

    [Theory]
    [InlineData(0)]     // end == start
    [InlineData(-30)]   // end before start
    public async Task Sleep_session_end_must_be_after_start(int minutes)
    {
        var response = await SyncOkAsync(Elder, Body(sleep: [Session("s-1", T0, T0.AddMinutes(minutes))]));

        Assert.Equal("INVALID_INTERVAL", ReasonAt(response, "sleepSessions", 0));
    }

    [Fact]
    public async Task Sleep_session_ending_in_the_future_is_rejected()
    {
        var response = await SyncOkAsync(Elder, Body(sleep: [Session("s-1", Now.AddHours(-2), Now.AddHours(5))]));

        Assert.Equal("TIME_IN_FUTURE", ReasonAt(response, "sleepSessions", 0));
    }

    [Theory]
    [InlineData(-5, 30)]    // starts before the session
    [InlineData(10, 500)]   // ends after the session
    public async Task A_stage_outside_its_session_rejects_the_whole_session(int startMinutes, int endMinutes)
    {
        var session = Session("s-1", T0, T0.AddHours(7), Stage(T0, T0.AddMinutes(30), "LIGHT"), Stage(T0.AddMinutes(startMinutes), T0.AddMinutes(endMinutes), "DEEP"));

        var response = await SyncOkAsync(Elder, Body(sleep: [session]));

        Assert.Equal("OUTSIDE_PARENT", ReasonAt(response, "sleepSessions", 0));
        Assert.Equal(0, Accepted(response, "sleepSessions"));
        Assert.Equal(0, await StoredRowCountAsync(Elder));
    }

    [Fact]
    public async Task A_stage_with_end_not_after_start_rejects_the_whole_session()
    {
        var session = Session("s-1", T0, T0.AddHours(7), Stage(T0.AddMinutes(20), T0.AddMinutes(20), "LIGHT"));

        Assert.Equal("INVALID_INTERVAL", ReasonAt(await SyncOkAsync(Elder, Body(sleep: [session])), "sleepSessions", 0));
    }

    [Fact]
    public async Task A_stage_with_a_missing_time_rejects_the_whole_session()
    {
        var session = Session("s-1", T0, T0.AddHours(7), With(Stage(T0, T0.AddMinutes(30), "LIGHT"), "end", null));

        Assert.Equal("MISSING_FIELD", ReasonAt(await SyncOkAsync(Elder, Body(sleep: [session])), "sleepSessions", 0));
    }

    [Fact]
    public async Task A_rejected_session_leaves_the_stored_one_untouched()
    {
        await SyncOkAsync(Elder, Body(sleep: [Session("s-1", T0, T0.AddHours(7), Stage(T0, T0.AddMinutes(30), "LIGHT"))]));

        await SyncOkAsync(Elder, Body(sleep: [Session("s-1", T0, T0.AddHours(7), Stage(T0, T0.AddHours(9), "DEEP"))]));

        using var _ = Factory.CreateDbScope(out var db);
        Assert.Equal(["LIGHT"], db.SleepStages.Select(s => s.Stage).ToList());
    }

    // ---- Deleted ----

    [Fact]
    public async Task Deleted_ids_are_validated_and_indexed_across_both_lists()
    {
        var deleted = new
        {
            heartRateRecords = new[] { "ok-1", "", "ok-2" },
            sleepSessions = new[] { "ok-3", new string('x', 129) },
        };

        var response = await SyncOkAsync(Elder, Body(deleted: deleted));

        var rejected = Rejected(response, "deleted");
        Assert.Equal(2, rejected.GetArrayLength());
        Assert.Equal(1, rejected[0].GetProperty("index").GetInt32());
        Assert.Equal("MISSING_FIELD", rejected[0].GetProperty("reason").GetString());
        Assert.Equal("heartRateRecords", rejected[0].GetProperty("block").GetString());
        // The sleep list continues the numbering after the 3 heart rate ids.
        Assert.Equal(4, rejected[1].GetProperty("index").GetInt32());
        Assert.Equal("FIELD_TOO_LONG", rejected[1].GetProperty("reason").GetString());
        Assert.Equal("sleepSessions", rejected[1].GetProperty("block").GetString());
        Assert.Equal(3, Accepted(response, "deleted"));
    }

    // ---- watchLastSeenAt ----

    [Fact]
    public async Task Watch_last_seen_in_the_future_is_rejected_and_the_stored_value_kept()
    {
        var stored = Now.AddMinutes(-30);
        await SyncOkAsync(Elder, Body(watchLastSeenAt: stored));

        var response = await SyncOkAsync(Elder, Body(watchLastSeenAt: Now.AddHours(2)));

        Assert.Equal("TIME_IN_FUTURE", Rejected(response, "watchLastSeenAt").GetString());
        var status = await ReadStatusAsync(Elder);
        Assert.InRange((ReadInstant(status, "watchLastSeenAt")!.Value - stored).Duration(), TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    // ---- Privacy ----

    [Fact]
    public async Task Rejections_never_echo_submitted_values()
    {
        var bad = With(Record("SECRET-RECORD-ID", T0, Sample(T0.AddMinutes(1), 301)), "origin", "secret.origin.package");
        var tooLong = With(Record("x", T0), "hcRecordId", new string('Z', 200));

        var response = await SyncAsync(Elder, Body(
            heartRate: [bad, tooLong, With(Record("SECRET-RECORD-ID-2", T0), "end", T0.AddDays(-1))],
            deleted: new { heartRateRecords = new[] { new string('Q', 200) } }));

        var text = await response.Content.ReadAsStringAsync(Ct);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("SECRET", text);
        Assert.DoesNotContain("secret.origin.package", text);
        Assert.DoesNotContain("ZZZZ", text);
        Assert.DoesNotContain("QQQQ", text);
    }

    // ---- Helpers ----

    /// <summary>Placeholders for strings too long to inline in an attribute.</summary>
    private static object? Expand(string? value) => value switch
    {
        "LONG129" => new string('x', 129),
        "LONG256" => new string('x', 256),
        _ => value,
    };
}
