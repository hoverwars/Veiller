using Veiller.Api.Data.Entities;
using Veiller.Api.Patients;

namespace Veiller.Api.Tests.Patients;

public sealed class HistoryWindowTests
{
    // Wednesday 2026-11-04 03:30 UTC = Tuesday 2026-11-03 22:30 at -05:00.
    private static readonly DateTime Now = new(2026, 11, 4, 3, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void Day_has_eight_three_hour_buckets_of_the_local_day()
    {
        var buckets = HistoryWindow.Buckets(HistoryRange.Day, Now, TimeSpan.FromHours(-5));

        Assert.Equal(["0h", "3h", "6h", "9h", "12h", "15h", "18h", "21h"], buckets.Select(b => b.Label));
        Assert.Equal(new DateTime(2026, 11, 3, 5, 0, 0), buckets[0].StartUtc); // local midnight of Nov 3
        Assert.Equal(new DateTime(2026, 11, 4, 5, 0, 0), buckets[^1].EndUtc);
    }

    [Fact]
    public void The_utc_day_differs_from_the_local_day_around_midnight()
    {
        Assert.Equal(new DateTime(2026, 11, 4), HistoryWindow.Buckets(HistoryRange.Day, Now, TimeSpan.Zero)[0].StartUtc);
    }

    [Fact]
    public void Week_ends_today_with_spanish_initials()
    {
        var buckets = HistoryWindow.Buckets(HistoryRange.Week, Now, TimeSpan.Zero);

        // Thursday Oct 29 .. Wednesday Nov 4, like the mock's J V S D L M X.
        Assert.Equal(["J", "V", "S", "D", "L", "M", "X"], buckets.Select(b => b.Label));
        Assert.Equal(new DateTime(2026, 10, 29), buckets[0].StartUtc);
    }

    [Fact]
    public void Month_has_thirty_days_and_a_label_every_fifth()
    {
        var buckets = HistoryWindow.Buckets(HistoryRange.Month, Now, TimeSpan.Zero);

        Assert.Equal(30, buckets.Count);
        Assert.Equal(new DateTime(2026, 10, 6), buckets[0].StartUtc);
        Assert.Equal(["6", "11", "16", "21", "26", "31"], buckets.Select(b => b.Label).Where(l => l != ""));
        Assert.Equal("", buckets[1].Label);
    }
}

public sealed class PatientMetricsTests
{
    private static readonly DateTime T = new(2026, 11, 3, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Resting_heart_rate_averages_the_lowest_quarter_of_the_minutes()
    {
        var samples = Enumerable.Range(0, 8).Select(i => (T.AddMinutes(i), 60 + i * 10)); // 60..130

        Assert.Equal(65f, PatientMetrics.RestingHeartRate(samples)); // lowest 2 of 8: 60 and 70
    }

    [Fact]
    public void Resting_heart_rate_is_null_without_samples() => Assert.Null(PatientMetrics.RestingHeartRate([]));

    [Fact]
    public void Samples_of_the_same_minute_are_averaged_first()
    {
        Assert.Equal(80f, PatientMetrics.RestingHeartRate([(T, 60), (T.AddSeconds(20), 100)]));
    }

    [Fact]
    public void Asleep_minutes_skip_awake_stages_and_fall_back_to_the_whole_session()
    {
        var session = new SleepSession { ElderId = "e", HcRecordId = "s", Origin = "o", Start = T, End = T.AddHours(8) };
        Assert.Equal(480, PatientMetrics.AsleepMinutes(session));

        session.Stages.Add(new SleepStage { ElderId = "e", HcRecordId = "s", Start = T, End = T.AddHours(1), Stage = "AWAKE" });
        session.Stages.Add(new SleepStage { ElderId = "e", HcRecordId = "s", Start = T.AddHours(1), End = T.AddHours(8), Stage = "DEEP" });
        Assert.Equal(420, PatientMetrics.AsleepMinutes(session));
    }

    [Fact]
    public void A_night_belongs_to_the_local_day_it_ends_on()
    {
        // Ends 04:00Z on Nov 3 = Nov 2 23:00 at -05:00, but 06:00 on Nov 3 at +02:00.
        var session = new SleepSession
        {
            ElderId = "e", HcRecordId = "s", Origin = "o", Start = T.AddHours(-8), End = T.AddHours(-4), ZoneOffset = "-05:00",
        };

        Assert.Equal(new DateOnly(2026, 11, 2), PatientMetrics.LocalDayOf(session, TimeSpan.Zero));
        session.ZoneOffset = null;
        Assert.Equal(new DateOnly(2026, 11, 3), PatientMetrics.LocalDayOf(session, TimeSpan.FromHours(2)));
    }
}
