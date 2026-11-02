namespace Veiller.Api.Consents;

public static class ConsentValidator
{
    /// <summary>
    /// The PUT body replaces the whole list: it must contain each known type exactly once,
    /// and required types cannot be refused. Returns the errors keyed by field, empty when valid.
    /// </summary>
    public static Dictionary<string, string[]> Validate(IReadOnlyList<ConsentItemRequest?>? items)
    {
        var errors = new Dictionary<string, string[]>();
        if (items is null)
        {
            errors["items"] = ["A list of consents is required."];
            return errors;
        }

        var seen = new HashSet<HealthDataType>();
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item is null)
            {
                errors[$"items[{i}]"] = ["The consent cannot be null."];
                continue;
            }

            if (item.Type is not { } type || !Enum.IsDefined(type))
                errors[$"items[{i}].type"] = ["A known health data type is required."];
            else if (!seen.Add(type))
                errors[$"items[{i}].type"] = [$"'{type}' appears more than once."];

            if (item.Granted is null)
                errors[$"items[{i}].granted"] = ["'granted' is required."];
            else if (!item.Granted.Value && item.Type is { } t && Enum.IsDefined(t) && t.IsRequired())
                errors[$"items[{i}].granted"] = [$"'{t}' is required and cannot be refused."];
        }

        var missing = HealthDataTypes.All.Where(t => !seen.Contains(t)).ToList();
        if (missing.Count > 0 && errors.Count == 0)
            errors["items"] = [$"Missing consents: {string.Join(", ", missing)}."];

        return errors;
    }
}
