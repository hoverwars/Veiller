using System.Text.Json;
using System.Text.Json.Serialization;

namespace Veiller.Api.Health;

/// <summary>
/// Reads the sync body by hand to enforce <see cref="HealthSyncOptions.MaxBodyBytes"/> (413) before parsing,
/// and to answer a ProblemDetails 400 for malformed JSON or wrong types.
/// </summary>
public static class HealthSyncBodyReader
{
    // The Web defaults read numbers from strings ("12"): the contract says a wrong type is a 400, so be strict.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { NumberHandling = JsonNumberHandling.Strict };

    public static async Task<(SyncRequest? Request, IResult? Error)> ReadAsync(HttpRequest http, long maxBytes, CancellationToken ct)
    {
        if (!http.HasJsonContentType())
            return (null, Results.Problem("The body must be application/json.", statusCode: StatusCodes.Status415UnsupportedMediaType));
        if (http.ContentLength > maxBytes) return (null, TooLarge());

        // Read at most one byte past the limit, which tells an oversized chunked body without buffering all of it.
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await http.Body.ReadAsync(chunk, ct)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > maxBytes) return (null, TooLarge());
        }

        try
        {
            var request = JsonSerializer.Deserialize<SyncRequest>(buffer.GetBuffer().AsSpan(0, (int)buffer.Length), JsonOptions);
            return request is null ? (null, Malformed()) : (request, null);
        }
        catch (JsonException)
        {
            // The exception message can quote a path or a position, never a health value; it is not forwarded anyway.
            return (null, Malformed());
        }
    }

    private static IResult Malformed() =>
        Results.Problem("The body is not a valid sync request (malformed JSON or wrong field types).", statusCode: StatusCodes.Status400BadRequest);

    public static IResult TooLarge() =>
        Results.Problem("The request is over a size limit. Nothing was stored.", statusCode: StatusCodes.Status413PayloadTooLarge);
}
