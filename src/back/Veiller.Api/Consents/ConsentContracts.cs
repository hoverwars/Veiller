namespace Veiller.Api.Consents;

/// <summary>Nullable members so a missing field is reported as a validation error instead of silently defaulting.</summary>
public sealed record ConsentItemRequest(HealthDataType? Type, bool? Granted);

public sealed record ConsentItemDto(HealthDataType Type, bool Granted);
