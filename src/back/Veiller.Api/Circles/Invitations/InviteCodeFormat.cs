using System.Security.Cryptography;

namespace Veiller.Api.Circles.Invitations;

/// <summary>Invitation codes look like <c>VLR-7K2P</c>. Mirrors the mobile <c>CircleCode</c> helper.</summary>
public static class InviteCodeFormat
{
    public const string Prefix = "VLR";
    public const int BodyLength = 4;

    /// <summary>No 0/O/1/I/L: characters that are easy to confuse when read aloud or typed.</summary>
    public const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    /// <summary>
    /// Normalizes user input (case, spaces, dash, optional prefix) to <c>VLR-XXXX</c>.
    /// Returns null when the result cannot be a code, so malformed input never reaches the database.
    /// </summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        var raw = new string(input.ToUpperInvariant().Where(char.IsAsciiLetterOrDigit).ToArray());
        var body = raw.StartsWith(Prefix, StringComparison.Ordinal) ? raw[Prefix.Length..] : raw;
        if (body.Length != BodyLength || !body.All(Alphabet.Contains)) return null;

        return $"{Prefix}-{body}";
    }

    public static string Generate() =>
        $"{Prefix}-{RandomNumberGenerator.GetString(Alphabet, BodyLength)}";
}
