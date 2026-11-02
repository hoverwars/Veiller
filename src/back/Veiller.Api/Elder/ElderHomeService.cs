using Microsoft.EntityFrameworkCore;
using Veiller.Api.Circles;
using Veiller.Api.Data;
using Veiller.Api.Data.Entities;
using Veiller.Api.Health;

namespace Veiller.Api.Elder;

public static class ElderHomeService
{
    public static async Task<ElderHomeDto> BuildAsync(
        VeillerDbContext db, TimeProvider clock, HealthSyncOptions options, string elderId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var status = await db.HealthSyncStatuses.AsNoTracking().SingleOrDefaultAsync(s => s.ElderId == elderId, ct);

        int? lastSendMinutesAgo = status is null ? null : MinutesSince(status.LastUploadAt, now);
        var connected = status?.WatchLastSeenAt is { } seen && now - AsUtc(seen) <= options.WatchConnectedWindow;

        return new ElderHomeDto(lastSendMinutesAgo, new WatchStatusDto(connected, BatteryPercent: null), await CaregiversAsync(db, elderId, ct));
    }

    private static async Task<IReadOnlyList<CircleMemberDto>> CaregiversAsync(VeillerDbContext db, string elderId, CancellationToken ct)
    {
        // The circle is created at onboarding; an elder without one simply has nobody following them yet.
        var members = await db.Circles.AsNoTracking()
            .Where(c => c.OwnerUserId == elderId)
            .SelectMany(c => c.Members)
            .Where(m => m.Role != MemberRole.DataOwner)
            .Include(m => m.User)
            .ToListAsync(ct);
        return members.OrderBy(m => m.JoinedAt).Select(CircleEndpoints.ToDto).ToList();
    }

    private static int MinutesSince(DateTime utc, DateTimeOffset now) => (int)Math.Max(0, (now - AsUtc(utc)).TotalMinutes);

    private static DateTimeOffset AsUtc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
