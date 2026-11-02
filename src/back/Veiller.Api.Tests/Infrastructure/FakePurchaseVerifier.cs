using Veiller.Api.Billing;

namespace Veiller.Api.Tests.Infrastructure;

/// <summary>Accepts only the purchase tokens a test declares valid (no store call).</summary>
public sealed class FakePurchaseVerifier : IPurchaseVerifier
{
    public HashSet<string> ValidTokens { get; } = [];

    /// <summary>When true, behaves like the real placeholder: no store is connected.</summary>
    public bool NotConnected { get; set; }

    public Task<bool> VerifyAsync(string purchaseToken, CancellationToken ct) =>
        NotConnected ? throw new NotImplementedException() : Task.FromResult(ValidTokens.Contains(purchaseToken));
}
