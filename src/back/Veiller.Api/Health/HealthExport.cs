using Microsoft.EntityFrameworkCore;
using Veiller.Api.Data;

namespace Veiller.Api.Health;

public sealed record ExportedSampleDto(DateTimeOffset Time, int Bpm);

public sealed record ExportedHeartRateRecordDto(
    string HcRecordId, string Origin, DateTimeOffset Start, DateTimeOffset End, IReadOnlyList<ExportedSampleDto> Samples);

public sealed record ExportedStepsDto(DateTimeOffset Start, DateTimeOffset End, int Count);

public sealed record ExportedStageDto(DateTimeOffset Start, DateTimeOffset End, string Stage);

public sealed record ExportedSleepSessionDto(
    string HcRecordId, string Origin, DateTimeOffset Start, DateTimeOffset End, string? ZoneOffset, IReadOnlyList<ExportedStageDto> Stages);

/// <summary>Every health measurement stored for an elder (right of access).</summary>
public sealed record HealthExportDto(
    HealthStatusDto Status,
    IReadOnlyList<ExportedHeartRateRecordDto> HeartRateRecords,
    IReadOnlyList<ExportedStepsDto> StepsHourly,
    IReadOnlyList<ExportedSleepSessionDto> SleepSessions);

public static class HealthExport
{
    public static async Task<HealthExportDto> ExportAsync(VeillerDbContext db, string elderId, CancellationToken ct)
    {
        var records = await db.HeartRateRecords.AsNoTracking()
            .Where(r => r.ElderId == elderId).Include(r => r.Samples).AsSplitQuery().ToListAsync(ct);
        var steps = await db.StepsHourly.AsNoTracking().Where(s => s.ElderId == elderId).ToListAsync(ct);
        var sessions = await db.SleepSessions.AsNoTracking()
            .Where(s => s.ElderId == elderId).Include(s => s.Stages).AsSplitQuery().ToListAsync(ct);

        // Ordered in memory after loading. The whole history is exported: there is no retention policy yet (feature 9), so this grows with time.
        return new HealthExportDto(
            await HealthSyncService.GetStatusAsync(db, elderId, ct),
            records.OrderBy(r => r.Start).Select(r => new ExportedHeartRateRecordDto(
                r.HcRecordId, r.Origin, r.Start.ToUtcOffset(), r.End.ToUtcOffset(),
                r.Samples.OrderBy(s => s.Time).Select(s => new ExportedSampleDto(s.Time.ToUtcOffset(), s.Bpm)).ToList())).ToList(),
            steps.OrderBy(s => s.Start).Select(s => new ExportedStepsDto(s.Start.ToUtcOffset(), s.End.ToUtcOffset(), s.Count)).ToList(),
            sessions.OrderBy(s => s.Start).Select(s => new ExportedSleepSessionDto(
                s.HcRecordId, s.Origin, s.Start.ToUtcOffset(), s.End.ToUtcOffset(), s.ZoneOffset,
                s.Stages.OrderBy(st => st.Ordinal).Select(st => new ExportedStageDto(st.Start.ToUtcOffset(), st.End.ToUtcOffset(), st.Stage)).ToList())).ToList());
    }
}
