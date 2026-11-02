using System.Net;
using Veiller.Api.Consents;
using AuthRole = Veiller.Api.Auth.Role;

namespace Veiller.Api.Tests.Patients;

public sealed class PatientHistoryTests : PatientApiTestBase
{
    private Task<Guid> SeedAsync() => SeedCircleAsync("elder-1", Caregiver);

    // ---- Input validation ----

    [Fact]
    public async Task Requires_authentication()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync($"/api/patients/{Guid.NewGuid()}/history?range=day", null)).StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?range=")]
    [InlineData("?range=year")]
    [InlineData("?range=1")]
    [InlineData("?range=day,week")]
    public async Task A_missing_or_unknown_range_is_a_400(string query)
    {
        var circle = await SeedAsync();

        var response = await GetAsync($"/api/patients/{circle}/history{query}", Caregiver);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("range", await response.Content.ReadAsStringAsync(Ct));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0200")]
    [InlineData("%2B25:00")]
    [InlineData("%2B02:60")]
    [InlineData("%2B٠٢:٠٠")] // Arabic-Indic digits
    [InlineData("")]
    public async Task A_malformed_tzOffset_is_a_400(string tzOffset)
    {
        var circle = await SeedAsync();

        var response = await GetAsync($"/api/patients/{circle}/history?range=week&tzOffset={tzOffset}", Caregiver);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("tzOffset", await response.Content.ReadAsStringAsync(Ct));
    }

    [Theory]
    [InlineData("range=day")]
    [InlineData("range=DAY")]
    [InlineData("range=week&tzOffset=-05:00")]
    [InlineData("range=week&tzOffset=%2B02:00")]
    [InlineData("range=month&tzOffset=+02:00")] // an unencoded plus arrives as a space
    [InlineData("range=month&tzOffset=%2B18:00")]
    public async Task Valid_queries_are_accepted(string query)
    {
        var circle = await SeedAsync();

        Assert.Equal(HttpStatusCode.OK, (await GetAsync($"/api/patients/{circle}/history?{query}", Caregiver)).StatusCode);
    }

    [Fact]
    public async Task A_stranger_gets_404_even_with_an_invalid_query()
    {
        var circle = await SeedAsync();
        await SeedUserAsync("stranger", AuthRole.Caregiver);

        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync($"/api/patients/{circle}/history?range=nope", "stranger")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync($"/api/patients/{circle}/history?range=day", "elder-1")).StatusCode);
    }

    // ---- Content ----

    [Fact]
    public async Task A_patient_without_data_has_empty_series()
    {
        var circle = await SeedAsync();

        foreach (var range in new[] { "day", "week", "month" })
        {
            var history = await GetOkAsync($"/api/patients/{circle}/history?range={range}", Caregiver);
            Assert.Empty(history.GetProperty("restingHeartRate").EnumerateArray());
            Assert.Empty(history.GetProperty("sleepHours").EnumerateArray());
        }
    }

    [Theory]
    [InlineData("day", 8)]
    [InlineData("week", 7)]
    [InlineData("month", 30)]
    public async Task The_heart_rate_series_has_one_point_per_bucket(string range, int expected)
    {
        var circle = await SeedAsync();
        await SyncHeartRateAsync("elder-1", "hr-1", Now.AddMinutes(-30), 70, 72);

        var history = await GetOkAsync($"/api/patients/{circle}/history?range={range}", Caregiver);

        Assert.Equal(range, history.GetProperty("range").GetString(), ignoreCase: true);
        var points = history.GetProperty("restingHeartRate").EnumerateArray().ToList();
        Assert.Equal(expected, points.Count);
        Assert.Contains(points, HasValue);
        Assert.All(points, p => Assert.True(p.TryGetProperty("timestamp", out _) && p.TryGetProperty("label", out _)));
    }

    [Fact]
    public async Task Overlapping_apps_do_not_count_the_same_minute_twice()
    {
        var circle = await SeedAsync();
        var start = Now.AddMinutes(-20);
        await SyncHeartRateAsync("elder-1", "app-a", start, 60);
        await SyncHeartRateAsync("elder-1", "app-b", start, 100);

        var points = (await GetOkAsync($"/api/patients/{circle}/history?range=month", Caregiver))
            .GetProperty("restingHeartRate").EnumerateArray().ToList();

        Assert.Equal(80f, Assert.Single(points, HasValue).GetProperty("value").GetSingle());
    }

    [Fact]
    public async Task Sleep_is_reported_in_hours_per_day_and_as_last_night_for_the_day_range()
    {
        var circle = await SeedAsync();
        await SyncOkAsync("elder-1", Body(sleep: [Session("sleep-1", Now.AddHours(-9), Now.AddHours(-3))]));

        var week = (await GetOkAsync($"/api/patients/{circle}/history?range=week&tzOffset=-05:00", Caregiver))
            .GetProperty("sleepHours").EnumerateArray().ToList();
        var day = (await GetOkAsync($"/api/patients/{circle}/history?range=day", Caregiver))
            .GetProperty("sleepHours").EnumerateArray().ToList();

        Assert.Equal(7, week.Count);
        Assert.Equal(6f, Assert.Single(week, HasValue).GetProperty("value").GetSingle());
        var night = Assert.Single(day);
        Assert.Equal("Anoche", night.GetProperty("label").GetString());
        Assert.Equal(6f, night.GetProperty("value").GetSingle());
    }

    [Fact]
    public async Task A_revoked_sleep_consent_empties_only_the_sleep_series()
    {
        var circle = await SeedAsync();
        await SyncHeartRateAsync("elder-1", "hr-1", Now.AddMinutes(-30), 70);
        await SyncOkAsync("elder-1", Body(sleep: [Session("sleep-1", Now.AddHours(-9), Now.AddHours(-3))]));
        await RevokeConsentAsync("elder-1", HealthDataType.Sleep);

        var history = await GetOkAsync($"/api/patients/{circle}/history?range=week", Caregiver);

        Assert.Empty(history.GetProperty("sleepHours").EnumerateArray());
        Assert.NotEmpty(history.GetProperty("restingHeartRate").EnumerateArray());
    }
}
