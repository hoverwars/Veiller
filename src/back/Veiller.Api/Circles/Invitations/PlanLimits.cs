using Veiller.Api.Data.Entities;

namespace Veiller.Api.Circles.Invitations;

/// <summary>Single source of truth for what each plan allows (reused by the future Premium feature).</summary>
public static class PlanLimits
{
    public static int MaxCaregivers(PlanTier plan) => plan switch
    {
        PlanTier.Premium => 8,
        _ => 2,
    };
}
