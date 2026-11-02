using System.Text.RegularExpressions;

namespace Veiller.Api.Health;

/// <summary>Parses the <c>±HH:MM</c> UTC offsets of the API (sleep <c>zoneOffset</c>, history <c>tzOffset</c>).</summary>
public static partial class UtcOffsets
{
    /// <summary>
    /// ASCII digits only (<c>int.Parse</c> rejects the Unicode ones <c>\d</c> would let through), within the ±18:00 range
    /// Health Connect (java.time.ZoneOffset) allows.
    /// </summary>
    public static bool TryParse(string? value, out TimeSpan offset)
    {
        offset = TimeSpan.Zero;
        if (value is null) return false;
        var m = OffsetPattern().Match(value.Trim());
        if (!m.Success) return false;
        var minutes = int.Parse(m.Groups[2].Value) * 60 + int.Parse(m.Groups[3].Value);
        if (minutes > 18 * 60) return false;
        offset = TimeSpan.FromMinutes(m.Groups[1].Value == "-" ? -minutes : minutes);
        return true;
    }

    [GeneratedRegex(@"^([+-])([0-9]{2}):([0-5][0-9])$")]
    private static partial Regex OffsetPattern();
}
