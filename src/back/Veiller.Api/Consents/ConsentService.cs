using Microsoft.EntityFrameworkCore;
using Veiller.Api.Data;
using Veiller.Api.Data.Entities;

namespace Veiller.Api.Consents;

public static class ConsentService
{
    /// <summary>The owner's consents: stored decisions, completed with the defaults for anything not saved yet.</summary>
    public static async Task<List<ConsentItemDto>> GetAsync(VeillerDbContext db, string ownerId, CancellationToken ct)
    {
        var stored = await db.Consents.AsNoTracking()
            .Where(c => c.UserId == ownerId)
            .ToDictionaryAsync(c => c.Type, c => c.Granted, ct);

        return HealthDataTypes.All
            .Select(t => new ConsentItemDto(t, stored.TryGetValue(t, out var granted) ? granted : t.GrantedByDefault()))
            .ToList();
    }

    /// <summary>Replaces the owner's consents with a validated, complete list and records when each one changed.</summary>
    public static async Task<List<ConsentItemDto>> ReplaceAsync(
        VeillerDbContext db, TimeProvider clock, string ownerId, IReadOnlyList<ConsentItemRequest> items, CancellationToken ct)
    {
        try
        {
            await ApplyAsync(db, clock, ownerId, items, ct);
        }
        catch (DbUpdateException)
        {
            // A concurrent first save may have inserted the same rows: replay once against fresh data (the PUT is idempotent).
            db.ChangeTracker.Clear();
            await ApplyAsync(db, clock, ownerId, items, ct);
        }

        return await GetAsync(db, ownerId, ct);
    }

    private static async Task ApplyAsync(
        VeillerDbContext db, TimeProvider clock, string ownerId, IReadOnlyList<ConsentItemRequest> items, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var existing = await db.Consents.Where(c => c.UserId == ownerId).ToDictionaryAsync(c => c.Type, ct);

        foreach (var item in items)
        {
            var type = item.Type!.Value;
            var granted = item.Granted!.Value;

            if (!existing.TryGetValue(type, out var consent))
            {
                db.Consents.Add(new Consent
                {
                    UserId = ownerId, Type = type, Granted = granted, UpdatedAt = now,
                    GrantedAt = granted ? now : null,
                });
                continue;
            }

            if (consent.Granted == granted) continue;
            consent.Granted = granted;
            consent.UpdatedAt = now;
            if (granted)
            {
                consent.GrantedAt = now;
                consent.RevokedAt = null;
            }
            else
            {
                consent.RevokedAt = now;
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
