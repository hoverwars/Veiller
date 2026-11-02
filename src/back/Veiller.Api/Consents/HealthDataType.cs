namespace Veiller.Api.Consents;

public enum HealthDataType
{
    HeartRate,
    Location,
    Sleep,
    ScreenTime,
    BloodPressure,
    Spo2,
    Ecg,
}

public static class HealthDataTypes
{
    /// <summary>Every consent type, in display order.</summary>
    public static readonly IReadOnlyList<HealthDataType> All = Enum.GetValues<HealthDataType>();

    /// <summary>Required types must always stay granted: the product cannot work without them.</summary>
    public static bool IsRequired(this HealthDataType type) =>
        type is HealthDataType.HeartRate or HealthDataType.Location or HealthDataType.Sleep or HealthDataType.ScreenTime;

    /// <summary>Value used before the owner has saved anything: everything granted except the ECG.</summary>
    public static bool GrantedByDefault(this HealthDataType type) => type != HealthDataType.Ecg;
}
