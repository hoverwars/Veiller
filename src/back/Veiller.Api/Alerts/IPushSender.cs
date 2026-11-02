namespace Veiller.Api.Alerts;

/// <summary>Data-only push: the app builds the text, so no health value or name travels through the push provider.</summary>
public sealed record PushMessage(IReadOnlyDictionary<string, string> Data);

/// <summary>Thin facade over Firebase Cloud Messaging, replaced by a fake in tests.</summary>
public interface IPushSender
{
    /// <summary>Sends the message to every token and returns the tokens the provider says are no longer valid.</summary>
    Task<IReadOnlyList<string>> SendAsync(IReadOnlyList<string> tokens, PushMessage message, CancellationToken ct);
}
