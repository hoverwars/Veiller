using Microsoft.EntityFrameworkCore;
using Veiller.Api.Auth;
using Veiller.Api.Consents;
using Veiller.Api.Data;
using Veiller.Api.Data.Entities;

namespace Veiller.Api.Profile;

public static class ProfileService
{
    public static ProfileDto ToDto(User u) => new(u.Id, u.FullName, u.Email, u.Role, u.Phone, u.Relation, u.BirthDate);

    /// <summary>Applies a validated partial update. Returns the new full name when it changed, null otherwise.</summary>
    public static string? Apply(User user, UpdateProfileRequest request)
    {
        string? renamed = null;
        if (request.FullName is { } name && name.Trim() != user.FullName)
        {
            user.FullName = name.Trim();
            renamed = user.FullName;
        }
        if (request.Phone is { } phone) user.Phone = NullIfBlank(phone);
        if (request.Relation is { } relation) user.Relation = NullIfBlank(relation);
        if (request.BirthDate is { } birthDate) user.BirthDate = birthDate;
        return renamed;
    }

    /// <summary>
    /// Removes the user's rows. Cascades remove their memberships and consents and, for an elder, their circle.
    /// A departing circle admin is replaced by the longest-standing caregiver (who inherits the sponsor flag).
    /// Returns false when there was no such user.
    /// </summary>
    public static async Task<bool> DeleteAsync(VeillerDbContext db, string userId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var leaving = await db.CircleMembers.AsNoTracking()
            .Where(m => m.UserId == userId && m.Role == MemberRole.Admin)
            .ToListAsync(ct);

        var deleted = await db.Users.Where(u => u.Id == userId).ExecuteDeleteAsync(ct) > 0;

        foreach (var admin in leaving)
        {
            // Ordered in memory (a circle has at most a handful of caregivers; SQLite cannot order by DateTimeOffset).
            var caregivers = await db.CircleMembers
                .Where(m => m.CircleId == admin.CircleId && m.Role == MemberRole.Caregiver)
                .ToListAsync(ct);
            var next = caregivers.OrderBy(m => m.JoinedAt).FirstOrDefault();
            if (next is null) continue;
            next.Role = MemberRole.Admin;
            next.IsSponsor |= admin.IsSponsor;
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return deleted;
    }

    public static async Task<ProfileExportDto> ExportAsync(VeillerDbContext db, User user, TimeProvider clock, CancellationToken ct)
    {
        var memberships = await db.CircleMembers.AsNoTracking()
            .Where(m => m.UserId == user.Id)
            .ToListAsync(ct);
        var ids = memberships.Select(m => m.CircleId).ToList();

        var circles = await db.Circles.AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .Include(c => c.Members).ThenInclude(m => m.User)
            .ToListAsync(ct);

        var exported = memberships.OrderBy(m => m.JoinedAt).Select(m =>
        {
            var circle = circles.Single(c => c.Id == m.CircleId);
            var owner = circle.Members.Single(x => x.Role == MemberRole.DataOwner).User!;
            return new ExportedCircleDto(
                circle.Id, m.Role, m.JoinedAt, owner.FullName, circle.Plan,
                circle.Members.OrderBy(x => x.JoinedAt).Select(x => new ExportedMemberDto(x.User!.FullName, x.Role)).ToList());
        }).ToList();

        // Consents belong to the elder (data owner); a caregiver has none of their own.
        var consents = user.Role == Role.Elder ? await ConsentService.GetAsync(db, user.Id, ct) : null;

        return new ProfileExportDto(clock.GetUtcNow(), user.CreatedAt, ToDto(user), consents, exported);
    }

    private static string? NullIfBlank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
