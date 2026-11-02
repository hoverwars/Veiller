using Veiller.Api.Data.Entities;

namespace Veiller.Api.Alerts;

public sealed record AlertDto(
    Guid Id, Guid CircleId, AlertType Type, AlertStatus Status, DateTimeOffset OpenedAt, DateTimeOffset? ResolvedAt, int? Value);

public sealed record AlertRuleDto(AlertType Type, bool Enabled, int Threshold, string Unit);

public sealed record AlertRulesDto(IReadOnlyList<AlertRuleDto> Rules);

/// <summary>Members are nullable so that a missing one is a 400 from the validator, not a failed deserialization.</summary>
public sealed record SaveAlertRulesRequest(List<AlertRuleItem?>? Rules);

public sealed record AlertRuleItem(AlertType? Type, bool? Enabled, int? Threshold);

public sealed record RegisterDeviceRequest(string? Token, string? Platform);

public sealed record UnregisterDeviceRequest(string? Token);
