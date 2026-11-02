using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Veiller.Api.Tests.Health;

/// <summary>Idempotence and the per-type storage semantics: replace for heart rate and sleep, upsert for steps, deletions last.</summary>
public sealed class HealthSyncStorageTests : HealthApiTestBase
{
    public HealthSyncStorageTests() => SeedUserAsync(Elder).GetAwaiter().GetResult();

    private static object[] ThreeSamples(DateTimeOffset start) =>
        [Sample(start.AddMinutes(1), 70), Sample(start.AddMinutes(2), 71), Sample(start.AddMinutes(3), 72)];

    private async Task<string> StoredStateAsync(string elderId)
    {
        using var _ = Factory.CreateDbScope(out var db);
        var state = new
        {
            records = (await db.HeartRateRecords.Where(r => r.ElderId == elderId).ToListAsync(Ct)).OrderBy(r => r.HcRecordId).Select(r => new { r.HcRecordId, r.Origin, r.Start, r.End }),
            samples = (await db.HeartRateSamples.Where(r => r.ElderId == elderId).ToListAsync(Ct)).OrderBy(r => r.HcRecordId).ThenBy(r => r.Time).Select(r => new { r.HcRecordId, r.Time, r.Bpm }),
            steps = (await db.StepsHourly.Where(r => r.ElderId == elderId).ToListAsync(Ct)).OrderBy(r => r.Start).Select(r => new { r.Start, r.End, r.Count }),
            sleep = (await db.SleepSessions.Where(r => r.ElderId == elderId).ToListAsync(Ct)).OrderBy(r => r.HcRecordId).Select(r => new { r.HcRecordId, r.Start, r.End, r.ZoneOffset, r.Origin }),
            stages = (await db.SleepStages.Where(r => r.ElderId == elderId).ToListAsync(Ct)).OrderBy(r => r.HcRecordId).ThenBy(r => r.Ordinal).Select(r => new { r.HcRecordId, r.Ordinal, r.Start, r.End, r.Stage }),
        };
        return JsonSerializer.Serialize(state);
    }

    // ---- Idempotence ----

    [Fact]
    public async Task Sending_the_same_request_twice_leaves_the_data_as_after_the_first_time()
    {
        var body = Body(
            heartRate: [Record("hr-1", T0, ThreeSamples(T0)), Record("hr-2", T0.AddHours(1), ThreeSamples(T0.AddHours(1)))],
            steps: [Hour(T0, 312), Hour(T0.AddHours(1), 0)],
            sleep: [Session("s-1", T0, T0.AddHours(7), Stage(T0, T0.AddHours(3), "LIGHT"), Stage(T0.AddHours(3), T0.AddHours(7), "DEEP"))],
            watchLastSeenAt: T0.AddHours(2));

        var first = await SyncOkAsync(Elder, body);
        var afterFirst = await StoredStateAsync(Elder);
        var second = await SyncOkAsync(Elder, body);

        Assert.Equal(afterFirst, await StoredStateAsync(Elder));
        // A repeated item counts as accepted, so the counters are identical.
        Assert.Equal(6, Accepted(first, "heartRateSamples"));
        Assert.Equal(6, Accepted(second, "heartRateSamples"));
        Assert.Equal(2, Accepted(second, "stepsHourly"));
        Assert.Equal(1, Accepted(second, "sleepSessions"));
        Assert.Equal(6, (await StoredSamplesAsync(Elder)).Count);
    }

    [Fact]
    public async Task New_and_repeated_items_can_be_mixed_in_one_request()
    {
        await SyncOkAsync(Elder, Body(heartRate: [Record("hr-1", T0, ThreeSamples(T0))], steps: [Hour(T0, 100)]));

        var response = await SyncOkAsync(Elder, Body(
            heartRate: [Record("hr-1", T0, ThreeSamples(T0)), Record("hr-2", T0.AddHours(1), ThreeSamples(T0.AddHours(1)))],
            steps: [Hour(T0, 100), Hour(T0.AddHours(1), 50)]));

        Assert.Equal(6, Accepted(response, "heartRateSamples"));
        Assert.Equal(2, Accepted(response, "stepsHourly"));
        Assert.Equal(6, (await StoredSamplesAsync(Elder)).Count);
    }

    // ---- Heart rate: replace ----

    [Fact]
    public async Task Resending_a_heart_rate_record_replaces_its_samples()
    {
        await SyncOkAsync(Elder, Body(heartRate: [Record("hr-1", T0, ThreeSamples(T0))]));

        // The app edited the record: one sample moved, one dropped, one new.
        await SyncOkAsync(Elder, Body(heartRate: [Record("hr-1", T0, Sample(T0.AddMinutes(2), 80), Sample(T0.AddMinutes(9), 90))]));

        var stored = await StoredSamplesAsync(Elder);
        Assert.Equal([80, 90], stored.Select(s => s.Bpm));
        Assert.Equal([T0.AddMinutes(2).UtcDateTime, T0.AddMinutes(9).UtcDateTime], stored.Select(s => s.Time));
    }

    [Fact]
    public async Task A_record_resent_without_samples_clears_them()
    {
        await SyncOkAsync(Elder, Body(heartRate: [Record("hr-1", T0, ThreeSamples(T0))]));

        var response = await SyncOkAsync(Elder, Body(heartRate: [Record("hr-1", T0)]));

        Assert.Equal(0, Accepted(response, "heartRateSamples"));
        Assert.Empty(await StoredSamplesAsync(Elder));
        using var _ = Factory.CreateDbScope(out var db);
        Assert.Equal(1, await db.HeartRateRecords.CountAsync(Ct));
    }

    [Fact]
    public async Task Replacing_one_record_does_not_touch_the_others()
    {
        await SyncOkAsync(Elder, Body(heartRate: [Record("hr-1", T0, ThreeSamples(T0)), Record("hr-2", T0.AddHours(1), ThreeSamples(T0.AddHours(1)))]));

        await SyncOkAsync(Elder, Body(heartRate: [Record("hr-1", T0, Sample(T0.AddMinutes(1), 99))]));

        var stored = await StoredSamplesAsync(Elder);
        Assert.Equal(4, stored.Count);
        Assert.Equal(3, stored.Count(s => s.RecordId == "hr-2"));
    }

    [Fact]
    public async Task Two_records_may_hold_a_sample_at_the_same_instant()
    {
        // Two apps writing the same minute: different hcRecordId, so both samples are kept.
        var response = await SyncOkAsync(Elder, Body(heartRate: [
            Record("app-a", T0, Sample(T0.AddMinutes(1), 70)), Record("app-b", T0, Sample(T0.AddMinutes(1), 71))]));

        Assert.Equal(2, Accepted(response, "heartRateSamples"));
        Assert.Equal(2, (await StoredSamplesAsync(Elder)).Count);
    }

    [Fact]
    public async Task Duplicate_record_id_in_one_request_keeps_the_last_occurrence()
    {
        var response = await SyncOkAsync(Elder, Body(heartRate: [
            Record("hr-1", T0, Sample(T0.AddMinutes(1), 60)), Record("hr-1", T0, Sample(T0.AddMinutes(2), 61), Sample(T0.AddMinutes(3), 62))]));

        Assert.Equal(2, Accepted(response, "heartRateSamples"));
        Assert.Equal([61, 62], (await StoredSamplesAsync(Elder)).Select(s => s.Bpm));
    }

    [Fact]
    public async Task Duplicate_sample_time_in_a_record_keeps_the_last_value()
    {
        var response = await SyncOkAsync(Elder, Body(heartRate: [Record("hr-1", T0, Sample(T0.AddMinutes(1), 60), Sample(T0.AddMinutes(1), 65))]));

        Assert.Equal(1, Accepted(response, "heartRateSamples"));
        Assert.Equal([65], (await StoredSamplesAsync(Elder)).Select(s => s.Bpm));
    }

    [Fact]
    public async Task Samples_are_stored_in_utc_whatever_the_offset_sent()
    {
        var offset = TimeSpan.FromHours(-5);
        var record = Record("hr-1", T0.ToOffset(offset), Sample(T0.AddMinutes(1).ToOffset(offset), 70));

        await SyncOkAsync(Elder, Body(heartRate: [record]));

        Assert.Equal(T0.AddMinutes(1).UtcDateTime, (await StoredSamplesAsync(Elder)).Single().Time);
    }

    // ---- Steps: upsert ----

    [Fact]
    public async Task Steps_bucket_is_upserted_and_the_last_value_wins_zero_included()
    {
        await SyncOkAsync(Elder, Body(steps: [Hour(T0, 300), Hour(T0.AddHours(1), 40)]));

        await SyncOkAsync(Elder, Body(steps: [Hour(T0, 350), Hour(T0.AddHours(1), 0)]));

        using var _ = Factory.CreateDbScope(out var db);
        var rows = (await db.StepsHourly.ToListAsync(Ct)).OrderBy(s => s.Start).ToList();
        Assert.Equal([350, 0], rows.Select(r => r.Count));
    }

    [Fact]
    public async Task Duplicate_bucket_in_one_request_keeps_the_last_value()
    {
        var response = await SyncOkAsync(Elder, Body(steps: [Hour(T0, 10), Hour(T0, 25)]));

        Assert.Equal(1, Accepted(response, "stepsHourly"));
        using var _ = Factory.CreateDbScope(out var db);
        Assert.Equal(25, (await db.StepsHourly.SingleAsync(Ct)).Count);
    }

    // ---- Sleep: replace with stages ----

    [Fact]
    public async Task Resending_a_sleep_session_replaces_it_and_its_stages()
    {
        await SyncOkAsync(Elder, Body(sleep: [Session("s-1", T0, T0.AddHours(7),
            Stage(T0, T0.AddHours(2), "LIGHT"), Stage(T0.AddHours(2), T0.AddHours(4), "DEEP"), Stage(T0.AddHours(4), T0.AddHours(7), "REM"))]));

        await SyncOkAsync(Elder, Body(sleep: [Session("s-1", T0, T0.AddHours(8), Stage(T0, T0.AddHours(8), "SLEEPING"))]));

        using var _ = Factory.CreateDbScope(out var db);
        Assert.Equal(T0.AddHours(8).UtcDateTime, (await db.SleepSessions.SingleAsync(Ct)).End);
        var stages = await db.SleepStages.ToListAsync(Ct);
        Assert.Equal("SLEEPING", Assert.Single(stages).Stage);
    }

    [Fact]
    public async Task Stages_keep_the_order_sent_and_unknown_values_become_unknown()
    {
        var stages = new object[]
        {
            Stage(T0, T0.AddHours(1), "AWAKE"), Stage(T0.AddHours(1), T0.AddHours(2), "out_of_bed"),
            Stage(T0.AddHours(2), T0.AddHours(3), "AWAKE_IN_BED"), Stage(T0.AddHours(3), T0.AddHours(4), "WHATEVER"),
            Stage(T0.AddHours(4), T0.AddHours(5), "1"), Stage(T0.AddHours(5), T0.AddHours(6), "REM"),
            With(Stage(T0.AddHours(6), T0.AddHours(7), "x"), "stage", null),
        };

        var response = await SyncOkAsync(Elder, Body(sleep: [Session("s-1", T0, T0.AddHours(7), stages)]));

        Assert.Equal(1, Accepted(response, "sleepSessions"));
        using var _ = Factory.CreateDbScope(out var db);
        var stored = (await db.SleepStages.ToListAsync(Ct)).OrderBy(s => s.Ordinal).Select(s => s.Stage);
        Assert.Equal(["AWAKE", "OUT_OF_BED", "AWAKE_IN_BED", "UNKNOWN", "UNKNOWN", "REM", "UNKNOWN"], stored);
    }

    [Fact]
    public async Task Sleep_session_without_stages_is_valid()
    {
        var session = With(Session("s-1", T0, T0.AddHours(7)), "stages", null);

        Assert.Equal(1, Accepted(await SyncOkAsync(Elder, Body(sleep: [session])), "sleepSessions"));
    }

    // ---- Deleted ----

    [Fact]
    public async Task Deleting_a_heart_rate_record_removes_it_and_its_samples()
    {
        await SyncOkAsync(Elder, Body(heartRate: [Record("hr-1", T0, ThreeSamples(T0)), Record("hr-2", T0.AddHours(1), ThreeSamples(T0.AddHours(1)))]));

        var response = await SyncOkAsync(Elder, Body(deleted: new { heartRateRecords = new[] { "hr-1" } }));

        Assert.Equal(1, Accepted(response, "deleted"));
        Assert.All(await StoredSamplesAsync(Elder), s => Assert.Equal("hr-2", s.RecordId));
        using var _ = Factory.CreateDbScope(out var db);
        Assert.Equal(["hr-2"], await db.HeartRateRecords.Select(r => r.HcRecordId).ToListAsync(Ct));
    }

    [Fact]
    public async Task Deleting_a_sleep_session_removes_it_and_its_stages()
    {
        await SyncOkAsync(Elder, Body(sleep: [Session("s-1", T0, T0.AddHours(7), Stage(T0, T0.AddHours(7), "LIGHT"))]));

        await SyncOkAsync(Elder, Body(deleted: new { sleepSessions = new[] { "s-1" } }));

        Assert.Equal(0, await StoredRowCountAsync(Elder));
    }

    [Fact]
    public async Task Deleting_an_unknown_id_is_not_an_error_and_can_be_repeated()
    {
        var body = Body(deleted: new { heartRateRecords = new[] { "nope" }, sleepSessions = new[] { "nope-either" } });

        var first = await SyncOkAsync(Elder, body);
        var second = await SyncOkAsync(Elder, body);

        Assert.Equal(2, Accepted(first, "deleted"));
        Assert.Equal(2, Accepted(second, "deleted"));
        Assert.Equal(0, Rejected(second, "deleted").GetArrayLength());
    }

    [Fact]
    public async Task An_id_upserted_and_deleted_in_the_same_request_ends_up_deleted()
    {
        var response = await SyncOkAsync(Elder, Body(
            heartRate: [Record("hr-1", T0, ThreeSamples(T0))],
            sleep: [Session("s-1", T0, T0.AddHours(7))],
            deleted: new { heartRateRecords = new[] { "hr-1" }, sleepSessions = new[] { "s-1" } }));

        Assert.Equal(3, Accepted(response, "heartRateSamples"));
        Assert.Equal(2, Accepted(response, "deleted"));
        Assert.Equal(0, await StoredRowCountAsync(Elder));
    }

    [Fact]
    public async Task Steps_have_no_deletions_a_zero_bucket_fixes_them()
    {
        await SyncOkAsync(Elder, Body(steps: [Hour(T0, 500)]));
        await SyncOkAsync(Elder, Body(steps: [Hour(T0, 0)]));

        using var _ = Factory.CreateDbScope(out var db);
        Assert.Equal(0, (await db.StepsHourly.SingleAsync(Ct)).Count);
    }

    // ---- Isolation between elders ----

    [Fact]
    public async Task Elders_with_the_same_record_ids_do_not_see_each_other()
    {
        await SeedUserAsync("elder-2");
        await SyncOkAsync(Elder, Body(heartRate: [Record("hr-1", T0, Sample(T0.AddMinutes(1), 70))], steps: [Hour(T0, 100)], sleep: [Session("s-1", T0, T0.AddHours(7))]));
        await SyncOkAsync("elder-2", Body(heartRate: [Record("hr-1", T0, Sample(T0.AddMinutes(1), 90))], steps: [Hour(T0, 900)], sleep: [Session("s-1", T0, T0.AddHours(6))]));

        Assert.Equal([70], (await StoredSamplesAsync(Elder)).Select(s => s.Bpm));
        Assert.Equal([90], (await StoredSamplesAsync("elder-2")).Select(s => s.Bpm));
        using var _ = Factory.CreateDbScope(out var db);
        Assert.Equal(100, (await db.StepsHourly.SingleAsync(s => s.ElderId == Elder, Ct)).Count);
        Assert.Equal(900, (await db.StepsHourly.SingleAsync(s => s.ElderId == "elder-2", Ct)).Count);
    }

    [Fact]
    public async Task An_elder_cannot_delete_the_records_of_another_elder()
    {
        await SeedUserAsync("elder-2");
        await SyncOkAsync("elder-2", Body(heartRate: [Record("hr-1", T0, Sample(T0.AddMinutes(1), 90))], sleep: [Session("s-1", T0, T0.AddHours(6))]));

        var response = await SyncOkAsync(Elder, Body(deleted: new { heartRateRecords = new[] { "hr-1" }, sleepSessions = new[] { "s-1" } }));

        Assert.Equal(2, Accepted(response, "deleted"));    // ids "processed", whether or not they existed for this elder
        Assert.Single(await StoredSamplesAsync("elder-2"));
        Assert.True(await StoredRowCountAsync("elder-2") > 1);
    }

    // ---- Atomicity ----

    [Fact]
    public async Task A_request_that_fails_in_storage_stores_nothing()
    {
        // Provoke a storage failure after the valid parts were written: the whole transaction rolls back.
        // An elder deleted between authentication and write is the realistic case: the foreign key rejects the insert.
        await SyncOkAsync(Elder, Body(steps: [Hour(T0, 1)]));
        using (var _ = Factory.CreateDbScope(out var db))
            await db.Database.ExecuteSqlRawAsync("DROP TABLE sleep_stages", Ct);

        var response = await SyncAsync(Elder, Body(
            steps: [Hour(T0, 999)],
            sleep: [Session("s-1", T0, T0.AddHours(7), Stage(T0, T0.AddHours(7), "LIGHT"))]));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        using var scope = Factory.CreateDbScope(out var check);
        Assert.Equal(1, (await check.StepsHourly.SingleAsync(Ct)).Count);
        Assert.Equal(0, await check.SleepSessions.CountAsync(Ct));
    }
}
