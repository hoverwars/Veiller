namespace Veiller.Api.Billing;

/// <summary>Checks with the store that a purchase token pays for an active Premium subscription.</summary>
public interface IPurchaseVerifier
{
    /// <returns>True when the store confirms an active Premium subscription for this token.</returns>
    /// <exception cref="NotImplementedException">No store is connected yet; the endpoint answers 501.</exception>
    Task<bool> VerifyAsync(string purchaseToken, CancellationToken ct);
}

/// <summary>
/// Placeholder for Google Play Billing (server-side receipt validation with the Play Developer API).
/// It needs a Play service account that does not exist yet, so subscribing answers 501 until it is implemented.
/// </summary>
public sealed class GooglePlayPurchaseVerifier : IPurchaseVerifier
{
    public Task<bool> VerifyAsync(string purchaseToken, CancellationToken ct) =>
        throw new NotImplementedException("Google Play receipt validation is not connected yet.");
}
