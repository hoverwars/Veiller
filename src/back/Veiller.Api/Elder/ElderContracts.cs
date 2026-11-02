using Veiller.Api.Circles;

namespace Veiller.Api.Elder;

/// <summary>Home screen of the elder: when data last reached the server, the watch status and the people following them.</summary>
/// <param name="LastSendMinutesAgo">Minutes since the last successful health sync; null when nothing was ever sent.</param>
/// <param name="Caregivers">Every circle member except the elder (the admin included), oldest first.</param>
public sealed record ElderHomeDto(int? LastSendMinutesAgo, WatchStatusDto Watch, IReadOnlyList<CircleMemberDto> Caregivers);

/// <param name="Connected">The watch was seen within <c>HealthSync:WatchConnectedWindow</c>.</param>
/// <param name="BatteryPercent">Always null: the battery is not part of the health sync contract yet.</param>
public sealed record WatchStatusDto(bool Connected, int? BatteryPercent);
