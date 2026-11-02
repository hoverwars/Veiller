using static Veiller.Api.Health.RejectionReason;

namespace Veiller.Api.Health;

/// <summary>
/// Per-item validation of a sync request. A bad item never fails the request: it is skipped and reported
/// (position and reason code only, never the value). Pure: no database, no clock besides <c>now</c>.
/// </summary>
public static class HealthSyncValidator
{
    /// <summary>Items counted against <see cref="HealthSyncOptions.MaxItemsPerRequest"/>: samples + steps buckets + sleep sessions + deleted ids.</summary>
    /// <remarks>Samples of a record over <paramref name="maxSamplesPerRecord"/> are not counted: that record is rejected as an item, not as a 413.</remarks>
    public static int CountItems(SyncRequest request, int maxSamplesPerRecord) =>
        (request.HeartRateRecords?.Sum(r => r?.Samples?.Count is { } n && n <= maxSamplesPerRecord ? n : 0) ?? 0)
        + (request.StepsHourly?.Count ?? 0)
        + (request.SleepSessions?.Count ?? 0)
        + (request.Deleted?.HeartRateRecords?.Count ?? 0)
        + (request.Deleted?.SleepSessions?.Count ?? 0);

    public static SyncPlan Validate(SyncRequest request, DateTimeOffset now, HealthSyncOptions options)
    {
        var plan = new SyncPlan();
        var window = new TimeWindow((now - options.MaxAge).UtcDateTime, (now + options.FutureTolerance).UtcDateTime);

        ValidateWatchLastSeenAt(request.WatchLastSeenAt, window.Latest, plan);
        ValidateHeartRate(request.HeartRateRecords, window, options.MaxSamplesPerRecord, plan);
        ValidateSteps(request.StepsHourly, window, plan);
        ValidateSleep(request.SleepSessions, window, plan);
        ValidateDeleted(request.Deleted, plan);
        return plan;
    }

    private static void ValidateWatchLastSeenAt(DateTimeOffset? value, DateTime limit, SyncPlan plan)
    {
        if (value is not { } seen) return;
        if (seen.UtcDateTime > limit) plan.RejectedWatchLastSeenAt = TimeInFuture;
        else plan.WatchLastSeenAt = seen.UtcDateTime;
    }

    /// <summary>Accepted range for the timestamps compared with the clock: [now - MaxAge, now + FutureTolerance].</summary>
    private readonly record struct TimeWindow(DateTime Earliest, DateTime Latest)
    {
        /// <summary>TIME_IN_FUTURE or TIME_TOO_OLD for a start/time outside the window, null otherwise.</summary>
        public string? Check(DateTime utc) => utc > Latest ? TimeInFuture : utc < Earliest ? TimeTooOld : null;
    }

    // ---- Heart rate ----

    private static void ValidateHeartRate(List<HeartRateRecordItem?>? records, TimeWindow limit, int maxSamples, SyncPlan plan)
    {
        if (records is null) return;

        // The last valid occurrence of a record id wins, like a client re-sending an edited record.
        var byId = new Dictionary<string, PlannedHeartRateRecord>(StringComparer.Ordinal);
        for (var i = 0; i < records.Count; i++)
        {
            var record = records[i];
            var rejection = record is null ? MissingField : CheckRecord(record, limit, maxSamples);
            if (rejection is not null)
            {
                plan.RejectedHeartRateRecords.Add(new RejectedItem(i, rejection));
                continue;
            }

            var start = record!.Start!.Value.UtcDateTime;
            var end = record.End!.Value.UtcDateTime;
            byId[record.HcRecordId!] = new PlannedHeartRateRecord(
                record.HcRecordId!, record.Origin!, start, end,
                ValidateSamples(i, record.Samples!, start, end, limit, plan));
        }

        plan.HeartRateRecords.AddRange(byId.Values);
    }

    /// <summary>A problem with the record itself (id, origin, interval) rejects it whole; its stored samples stay untouched.</summary>
    private static string? CheckRecord(HeartRateRecordItem r, TimeWindow limit, int maxSamples)
    {
        // A record without a samples array is rejected rather than read as "no samples": replace semantics would wipe the stored ones.
        if (IsBlank(r.HcRecordId) || IsBlank(r.Origin) || r.Start is null || r.End is null || r.Samples is null) return MissingField;
        if (r.HcRecordId!.Length > HealthLimits.MaxIdLength || r.Origin!.Length > HealthLimits.MaxOriginLength) return FieldTooLong;
        if (r.End < r.Start) return InvalidInterval;
        // Only the start is checked: watch apps write blocks whose end is still in the future (the samples are checked one by one).
        if (limit.Check(r.Start.Value.UtcDateTime) is { } timeProblem) return timeProblem;
        if (r.Samples.Count > maxSamples) return TooManySamples;
        return null;
    }

    private static List<PlannedSample> ValidateSamples(
        int recordIndex, List<HeartRateSampleItem?> samples, DateTime start, DateTime end, TimeWindow limit, SyncPlan plan)
    {
        // Several samples at the same instant collapse into the last one: (record, time) is the key.
        var byTime = new Dictionary<DateTime, int>();
        for (var j = 0; j < samples.Count; j++)
        {
            var rejection = CheckSample(samples[j], start, end, limit);
            if (rejection is not null)
            {
                plan.RejectedHeartRateRecords.Add(new RejectedItem(recordIndex, rejection, SampleIndex: j));
                continue;
            }
            byTime[samples[j]!.Time!.Value.UtcDateTime] = (int)samples[j]!.Bpm!.Value;
        }

        return byTime.OrderBy(kv => kv.Key).Select(kv => new PlannedSample(kv.Key, kv.Value)).ToList();
    }

    private static string? CheckSample(HeartRateSampleItem? s, DateTime start, DateTime end, TimeWindow limit)
    {
        if (s?.Time is not { } time || s.Bpm is not { } bpm) return MissingField;
        if (bpm is < HealthLimits.MinBpm or > HealthLimits.MaxBpm) return BpmOutOfRange;
        var utc = time.UtcDateTime;
        if (limit.Check(utc) is { } timeProblem) return timeProblem;
        if (utc < start || utc > end) return OutsideParent;
        return null;
    }

    // ---- Steps ----

    private static void ValidateSteps(List<StepsBucketItem?>? buckets, TimeWindow limit, SyncPlan plan)
    {
        if (buckets is null) return;

        // Last value received wins per hour: the client re-sends recent buckets with updated counts.
        var byStart = new Dictionary<DateTime, PlannedStepsHour>();
        for (var i = 0; i < buckets.Count; i++)
        {
            var rejection = CheckBucket(buckets[i], limit);
            if (rejection is not null)
            {
                plan.RejectedStepsHourly.Add(new RejectedItem(i, rejection));
                continue;
            }
            var start = buckets[i]!.Start!.Value.UtcDateTime;
            byStart[start] = new PlannedStepsHour(start, (int)buckets[i]!.Count!.Value);
        }

        plan.StepsHourly.AddRange(byStart.Values);
    }

    private static string? CheckBucket(StepsBucketItem? b, TimeWindow limit)
    {
        if (b?.Start is not { } start || b.End is not { } end || b.Count is not { } count) return MissingField;
        if (end <= start) return InvalidInterval;
        var s = start.UtcDateTime;
        // The bucket of the current hour is legitimate and ends in the future: only its start is compared with the clock.
        if (limit.Check(s) is { } timeProblem) return timeProblem;
        if (s.Ticks % TimeSpan.TicksPerHour != 0 || end.UtcDateTime - s != TimeSpan.FromHours(1)) return BucketNotAligned;
        if (count is < 0 or > HealthLimits.MaxStepsPerHour) return CountOutOfRange;
        return null;
    }

    // ---- Sleep ----

    private static void ValidateSleep(List<SleepSessionItem?>? sessions, TimeWindow limit, SyncPlan plan)
    {
        if (sessions is null) return;

        var byId = new Dictionary<string, PlannedSleepSession>(StringComparer.Ordinal);
        for (var i = 0; i < sessions.Count; i++)
        {
            var session = sessions[i];
            var rejection = session is null ? MissingField : CheckSession(session, limit);
            if (rejection is not null)
            {
                plan.RejectedSleepSessions.Add(new RejectedItem(i, rejection));
                continue;
            }

            var start = session!.Start!.Value.UtcDateTime;
            var end = session.End!.Value.UtcDateTime;
            var stages = ValidateStages(session.Stages, start, end, out var stageRejection);
            if (stageRejection is not null)
            {
                // A bad stage rejects its whole session.
                plan.RejectedSleepSessions.Add(new RejectedItem(i, stageRejection));
                continue;
            }

            byId[session.HcRecordId!] = new PlannedSleepSession(
                session.HcRecordId!, session.Origin!, start, end, NullIfBlank(session.ZoneOffset), stages);
        }

        plan.SleepSessions.AddRange(byId.Values);
    }

    private static string? CheckSession(SleepSessionItem s, TimeWindow limit)
    {
        // zoneOffset is optional: Health Connect itself allows a null offset.
        if (IsBlank(s.HcRecordId) || IsBlank(s.Origin) || s.Start is null || s.End is null) return MissingField;
        if (s.HcRecordId!.Length > HealthLimits.MaxIdLength || s.Origin!.Length > HealthLimits.MaxOriginLength) return FieldTooLong;
        if (s.End <= s.Start) return InvalidInterval;
        if (s.End.Value.UtcDateTime > limit.Latest) return TimeInFuture;
        if (s.Start.Value.UtcDateTime < limit.Earliest) return TimeTooOld;
        if (!IsBlank(s.ZoneOffset) && !IsValidOffset(s.ZoneOffset!)) return InvalidFormat;
        return null;
    }

    private static List<PlannedStage> ValidateStages(List<SleepStageItem?>? stages, DateTime start, DateTime end, out string? rejection)
    {
        rejection = null;
        var planned = new List<PlannedStage>(stages?.Count ?? 0);
        if (stages is null) return planned;

        foreach (var stage in stages)
        {
            if (stage?.Start is not { } s || stage.End is not { } e) { rejection = MissingField; return []; }
            if (e <= s) { rejection = InvalidInterval; return []; }
            if (s.UtcDateTime < start || e.UtcDateTime > end) { rejection = OutsideParent; return []; }
            planned.Add(new PlannedStage(s.UtcDateTime, e.UtcDateTime, SleepStages.Normalize(stage.Stage)));
        }
        return planned;
    }

    // ---- Deleted ----

    private static void ValidateDeleted(DeletedItems? deleted, SyncPlan plan)
    {
        if (deleted is null) return;
        var heartRateCount = deleted.HeartRateRecords?.Count ?? 0;
        ValidateIds(deleted.HeartRateRecords, offset: 0, "heartRateRecords", plan.DeletedHeartRateRecords, plan);
        // The index of a sleep id continues after the heart rate ids; `block` tells which list it belongs to.
        ValidateIds(deleted.SleepSessions, heartRateCount, "sleepSessions", plan.DeletedSleepSessions, plan);
    }

    private static void ValidateIds(List<string?>? ids, int offset, string block, HashSet<string> target, SyncPlan plan)
    {
        if (ids is null) return;
        for (var i = 0; i < ids.Count; i++)
        {
            if (IsBlank(ids[i])) plan.RejectedDeleted.Add(new RejectedItem(offset + i, MissingField, Block: block));
            else if (ids[i]!.Length > HealthLimits.MaxIdLength) plan.RejectedDeleted.Add(new RejectedItem(offset + i, FieldTooLong, Block: block));
            else target.Add(ids[i]!);
        }
    }

    // ---- Helpers ----

    private static bool IsBlank(string? value) => string.IsNullOrWhiteSpace(value);

    private static string? NullIfBlank(string? value) => IsBlank(value) ? null : value!.Trim();

    private static bool IsValidOffset(string value) => UtcOffsets.TryParse(value, out _);
}
