using Veiller.Api.Health;

namespace Veiller.Api.Patients;

/// <summary>Validation of the history query string. Errors come back as a standard validation problem (400).</summary>
public static class PatientQueryValidator
{
    public static (HistoryRange Range, TimeSpan Offset, IResult? Error) ParseHistory(string? range, string? tzOffset)
    {
        var errors = new Dictionary<string, string[]>();

        HistoryRange parsed = default;
        switch (range?.Trim().ToLowerInvariant())
        {
            case "day": parsed = HistoryRange.Day; break;
            case "week": parsed = HistoryRange.Week; break;
            case "month": parsed = HistoryRange.Month; break;
            default: errors["range"] = ["range must be one of: day, week, month."]; break;
        }

        // An unencoded "+" in a query string reaches the server as a space: read it as the plus sign it was meant to be.
        var offset = TimeSpan.Zero;
        if (tzOffset is not null)
        {
            var candidate = tzOffset.StartsWith(' ') ? "+" + tzOffset.TrimStart() : tzOffset;
            if (!UtcOffsets.TryParse(candidate, out offset))
                errors["tzOffset"] = ["tzOffset must look like +02:00 or -05:00 (within ±18:00)."];
        }

        return (parsed, offset, errors.Count > 0 ? Results.ValidationProblem(errors) : null);
    }
}
