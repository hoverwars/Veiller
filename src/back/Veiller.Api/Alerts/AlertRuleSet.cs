using Veiller.Api.Data.Entities;

namespace Veiller.Api.Alerts;

/// <summary>The effective rules of a circle: its overrides on top of the defaults. Also validates a save request.</summary>
public sealed class AlertRuleSet
{
    private readonly Dictionary<AlertType, (bool Enabled, int Threshold)> _rules;

    private AlertRuleSet(Dictionary<AlertType, (bool, int)> rules) => _rules = rules;

    public static IReadOnlyList<AlertType> Types { get; } = Enum.GetValues<AlertType>();

    public static AlertRuleSet Defaults(AlertOptions options) => new(new()
    {
        [AlertType.HeartRateHigh] = (true, options.HeartRateHigh),
        [AlertType.HeartRateLow] = (true, options.HeartRateLow),
        [AlertType.SleepLow] = (true, options.SleepLowMinutes),
        [AlertType.NoSync] = (true, options.NoSyncMinutes),
    });

    public static AlertRuleSet From(AlertOptions options, IEnumerable<AlertRule> overrides)
    {
        var set = Defaults(options);
        foreach (var rule in overrides) set._rules[rule.Type] = (rule.Enabled, rule.Threshold);
        return set;
    }

    public bool IsEnabled(AlertType type) => _rules[type].Enabled;

    public int Threshold(AlertType type) => _rules[type].Threshold;

    public IReadOnlyList<AlertRuleDto> ToDtos() =>
        Types.Select(t => new AlertRuleDto(t, _rules[t].Enabled, _rules[t].Threshold, UnitOf(t))).ToList();

    public static string UnitOf(AlertType type) => type is AlertType.HeartRateHigh or AlertType.HeartRateLow ? "bpm" : "minutes";

    /// <summary>Accepted threshold range per type (plausibility of what a caregiver can configure).</summary>
    public static (int Min, int Max) RangeOf(AlertType type) => type switch
    {
        AlertType.HeartRateHigh => (60, 250),
        AlertType.HeartRateLow => (20, 100),
        AlertType.SleepLow => (60, 720),
        _ => (30, 2880),
    };

    /// <summary>Applies a validated save request on top of the current rules and checks the result is consistent.</summary>
    public AlertRuleSet With(IEnumerable<(AlertType Type, bool Enabled, int Threshold)> changes)
    {
        var copy = new AlertRuleSet(new Dictionary<AlertType, (bool, int)>(_rules));
        foreach (var (type, enabled, threshold) in changes) copy._rules[type] = (enabled, threshold);
        return copy;
    }

    public bool HeartRateBoundsAreConsistent =>
        Threshold(AlertType.HeartRateHigh) > Threshold(AlertType.HeartRateLow);

    /// <summary>Field errors of a save request (empty when valid). Reports positions and field names, never echoes values.</summary>
    public static Dictionary<string, string[]> Validate(SaveAlertRulesRequest? request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request?.Rules is not { Count: > 0 } rules)
        {
            errors["rules"] = ["At least one rule is required."];
            return errors;
        }

        var seen = new HashSet<AlertType>();
        for (var i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            if (rule?.Type is not { } type || !Enum.IsDefined(type)) { errors[$"rules[{i}].type"] = ["A known alert type is required."]; continue; }
            if (!seen.Add(type)) errors[$"rules[{i}].type"] = ["This type appears twice."];
            if (rule.Enabled is null) errors[$"rules[{i}].enabled"] = ["Required."];
            if (rule.Threshold is not { } threshold) { errors[$"rules[{i}].threshold"] = ["Required."]; continue; }

            var (min, max) = RangeOf(type);
            if (threshold < min || threshold > max)
                errors[$"rules[{i}].threshold"] = [$"Must be between {min} and {max} {UnitOf(type)}."];
        }
        return errors;
    }
}
