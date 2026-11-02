using Veiller.Api.Alerts;

namespace Veiller.Api.Tests.Infrastructure;

/// <summary>Records the pushes instead of calling Firebase Cloud Messaging.</summary>
public sealed class FakePushSender : IPushSender
{
    public List<(IReadOnlyList<string> Tokens, PushMessage Message)> Sent { get; } = [];

    /// <summary>Tokens the fake provider reports as no longer valid.</summary>
    public HashSet<string> InvalidTokens { get; } = [];

    /// <summary>When set, SendAsync throws it (simulates FCM being unreachable).</summary>
    public Exception? Failure { get; set; }

    public Task<IReadOnlyList<string>> SendAsync(IReadOnlyList<string> tokens, PushMessage message, CancellationToken ct)
    {
        if (Failure is not null) throw Failure;
        Sent.Add((tokens, message));
        return Task.FromResult<IReadOnlyList<string>>(tokens.Where(InvalidTokens.Contains).ToList());
    }
}
