using Microsoft.EntityFrameworkCore;
using Veiller.Api.Circles;
using Veiller.Api.Consents;
using Veiller.Api.Data;
using Veiller.Api.Data.Entities;

namespace Veiller.Api.Patients;

public static class PatientSummaryService
{
    /// <summary>A reading older than this is no longer "current" and is reported as null.</summary>
    private static readonly TimeSpan Freshness = TimeSpan.FromHours(24);

    /// <summary>The circle (with its members) a caregiver follows, or null when they are not a caregiver of it (callers answer 404).</summary>
    public static async Task<Circle?> FindFollowedCircleAsync(VeillerDbContext db, Guid circleId, string uid, CancellationToken ct)
    {
        var membership = await db.FindMembershipAsync(circleId, uid, ct);
        if (membership is null || membership.Role == MemberRole.DataOwner) return null;
        return await LoadCirclesAsync(db, db.Circles.Where(c => c.Id == circleId), ct) is [var circle] ? circle : null;
    }

    /// <summary>Circles the user follows as a caregiver, ordered by owner name.</summary>
    public static async Task<List<Circle>> ListFollowedCirclesAsync(VeillerDbContext db, string uid, CancellationToken ct)
    {
        var ids = db.CircleMembers.Where(m => m.UserId == uid && m.Role != MemberRole.DataOwner).Select(m => m.CircleId);
        var circles = await LoadCirclesAsync(db, db.Circles.Where(c => ids.Contains(c.Id)), ct);
        return circles.OrderBy(c => OwnerOf(c).FullName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public static User OwnerOf(Circle circle) => circle.Members.Single(m => m.Role == MemberRole.DataOwner).User!;

    private static Task<List<Circle>> LoadCirclesAsync(VeillerDbContext db, IQueryable<Circle> query, CancellationToken ct) =>
        query.AsNoTracking().Include(c => c.Members).ThenInclude(m => m.User).ToListAsync(ct);

    public static async Task<PatientSummaryDto> BuildAsync(VeillerDbContext db, TimeProvider clock, Circle circle, CancellationToken ct)
    {
        var owner = OwnerOf(circle);
        var now = clock.GetUtcNow();
        var nowUtc = now.UtcDateTime;
        var consents = await ConsentService.GetAsync(db, owner.Id, ct);
        bool Granted(HealthDataType type) => consents.Single(c => c.Type == type).Granted;

        int? heartRate = null;
        if (Granted(HealthDataType.HeartRate))
        {
            var since = nowUtc - Freshness;
            heartRate = await db.HeartRateSamples.AsNoTracking()
                .Where(s => s.ElderId == owner.Id && s.Time > since && s.Time <= nowUtc)
                .OrderByDescending(s => s.Time)
                .Select(s => (int?)s.Bpm)
                .FirstOrDefaultAsync(ct);
        }

        var sleepMinutes = Granted(HealthDataType.Sleep) ? await LastNightMinutesAsync(db, owner.Id, nowUtc, ct) : null;

        var status = await db.HealthSyncStatuses.AsNoTracking().SingleOrDefaultAsync(s => s.ElderId == owner.Id, ct);
        DateTimeOffset? lastSync = status is null ? null : new DateTimeOffset(DateTime.SpecifyKind(status.WatchLastSeenAt ?? status.LastUploadAt, DateTimeKind.Utc));
        var minutesAgo = lastSync is { } at ? (int)Math.Max(0, (now - at).TotalMinutes) : 0;

        return new PatientSummaryDto(
            circle.Id, owner.FullName, CircleService.AgeOf(owner.BirthDate, clock), circle.Plan,
            circle.Members.Count(m => m.Role != MemberRole.DataOwner),
            heartRate, sleepMinutes, Spo2Percent: null, BloodPressure: null, BatteryPercent: null,
            minutesAgo, lastSync);
    }

    /// <summary>The most recent night, if it ended less than a day ago: all sessions of the day the latest one ends on.</summary>
    public static async Task<int?> LastNightMinutesAsync(VeillerDbContext db, string elderId, DateTime nowUtc, CancellationToken ct)
    {
        var sessions = await db.SleepSessions.AsNoTracking().Include(s => s.Stages)
            .Where(s => s.ElderId == elderId && s.End > nowUtc - Freshness && s.End <= nowUtc)
            .ToListAsync(ct);
        if (sessions.Count == 0) return null;

        var lastDay = PatientMetrics.LocalDayOf(sessions.MaxBy(s => s.End)!, TimeSpan.Zero);
        return sessions.Where(s => PatientMetrics.LocalDayOf(s, TimeSpan.Zero) == lastDay).Sum(PatientMetrics.AsleepMinutes);
    }
}
