namespace Veiller.Api.Data.Entities;

/// <summary>A Firebase Cloud Messaging registration token of one device of a user.</summary>
public sealed class DeviceToken
{
    public required string Token { get; init; }

    public required string UserId { get; set; }

    public required string Platform { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
