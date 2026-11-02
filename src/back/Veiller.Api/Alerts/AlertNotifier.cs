using Microsoft.EntityFrameworkCore;
using Veiller.Api.Data;
using Veiller.Api.Data.Entities;

namespace Veiller.Api.Alerts;

/// <summary>Pushes a newly opened alert to the caregivers of the circle. A push failure never fails the sync or the evaluation.</summary>
public sealed class AlertNotifier(VeillerDbContext db, IPushSender sender, ILogger<AlertNotifier> logger)
{
    public async Task NotifyAsync(IReadOnlyCollection<Alert> alerts, CancellationToken ct)
    {
        foreach (var alert in alerts)
        {
            try
            {
                var recipients = db.CircleMembers.Where(m => m.CircleId == alert.CircleId && m.Role != MemberRole.DataOwner).Select(m => m.UserId);
                var tokens = await db.DeviceTokens.AsNoTracking().Where(t => recipients.Contains(t.UserId)).Select(t => t.Token).ToListAsync(ct);
                if (tokens.Count == 0) continue;

                var message = new PushMessage(new Dictionary<string, string>
                {
                    ["kind"] = "alert",
                    ["alertId"] = alert.Id.ToString(),
                    ["circleId"] = alert.CircleId.ToString(),
                    ["type"] = alert.Type.ToString(),
                });
                var invalid = await sender.SendAsync(tokens, message, ct);
                if (invalid.Count > 0) await db.DeviceTokens.Where(t => invalid.Contains(t.Token)).ExecuteDeleteAsync(ct);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogWarning(e, "Push notification of an alert failed");
            }
        }
    }
}
