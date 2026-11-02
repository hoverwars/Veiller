using System.Net;
using Veiller.Api.Consents;
using AuthRole = Veiller.Api.Auth.Role;

namespace Veiller.Api.Tests.Patients;

public sealed class PatientSummaryTests : PatientApiTestBase
{
    // ---- Authorization and input ----

    [Fact]
    public async Task Requires_authentication()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync("/api/patients", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync($"/api/patients/{Guid.NewGuid()}", null)).StatusCode);
    }

    [Fact]
    public async Task Detail_with_a_malformed_circle_id_is_not_found()
    {
        await SeedUserAsync(Caregiver, AuthRole.Caregiver);
        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync("/api/patients/not-a-guid", Caregiver)).StatusCode);
    }

    [Fact]
    public async Task A_stranger_gets_404_not_403()
    {
        var circle = await SeedCircleAsync("elder-1", Caregiver);
        await SeedUserAsync("stranger", AuthRole.Caregiver);

        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync($"/api/patients/{circle}", "stranger")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync($"/api/patients/{Guid.NewGuid()}", Caregiver)).StatusCode);
    }

    [Fact]
    public async Task The_elder_cannot_read_their_own_patient_view()
    {
        var circle = await SeedCircleAsync("elder-1", Caregiver);

        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync($"/api/patients/{circle}", "elder-1")).StatusCode);
        Assert.Empty((await GetOkAsync("/api/patients", "elder-1")).EnumerateArray());
    }

    [Fact]
    public async Task The_list_only_contains_circles_the_caregiver_belongs_to()
    {
        var mine = await SeedCircleAsync("elder-1", Caregiver);
        await SeedCircleAsync("elder-2", "caregiver-2");

        var list = await GetOkAsync("/api/patients", Caregiver);

        Assert.Equal(mine, Guid.Parse(Assert.Single(list.EnumerateArray()).GetProperty("circleId").GetString()!));
    }

    // ---- Content ----

    [Fact]
    public async Task A_patient_who_never_synced_has_null_vitals_and_no_error()
    {
        var circle = await SeedCircleAsync("elder-1", Caregiver, "caregiver-2");

        var patient = await GetOkAsync($"/api/patients/{circle}", Caregiver);

        Assert.Equal("Name elder-1", patient.GetProperty("fullName").GetString());
        Assert.Equal(2, patient.GetProperty("caregiverCount").GetInt32());
        Assert.Equal("Free", patient.GetProperty("plan").GetString());
        foreach (var field in new[] { "heartRateBpm", "sleepMinutes", "spo2Percent", "bloodPressure", "batteryPercent", "lastSyncAt", "age" })
            Assert.True(IsNull(patient, field), field);
        Assert.Equal(0, patient.GetProperty("lastSyncMinutesAgo").GetInt32());
    }

    [Fact]
    public async Task Synced_data_shows_the_latest_heart_rate_the_night_and_the_sync_age()
    {
        var circle = await SeedCircleAsync("elder-1", Caregiver);
        await SyncHeartRateAsync("elder-1", "hr-1", Now.AddMinutes(-30), 70, 72, 128);
        var now = Now; // one instant: stages must line up exactly with the session
        await SyncOkAsync("elder-1", Body(sleep:
        [
            Session("sleep-1", now.AddHours(-9), now.AddHours(-2),
                Stage(now.AddHours(-9), now.AddHours(-8), "AWAKE"),
                Stage(now.AddHours(-8), now.AddHours(-2), "LIGHT")),
        ]));

        var patient = await GetOkAsync($"/api/patients/{circle}", Caregiver);

        Assert.Equal(128, patient.GetProperty("heartRateBpm").GetInt32());
        Assert.Equal(360, patient.GetProperty("sleepMinutes").GetInt32()); // the awake hour is not counted
        Assert.InRange(patient.GetProperty("lastSyncMinutesAgo").GetInt32(), 0, 2);
        Assert.False(IsNull(patient, "lastSyncAt"));
        Assert.True(IsNull(patient, "spo2Percent"));
        Assert.True(IsNull(patient, "bloodPressure"));
        Assert.True(IsNull(patient, "batteryPercent"));
    }

    [Fact]
    public async Task The_watch_liveness_signal_wins_over_the_upload_time()
    {
        var circle = await SeedCircleAsync("elder-1", Caregiver);
        await SyncOkAsync("elder-1", Body(watchLastSeenAt: Now.AddMinutes(-45)));

        var patient = await GetOkAsync($"/api/patients/{circle}", Caregiver);

        Assert.InRange(patient.GetProperty("lastSyncMinutesAgo").GetInt32(), 44, 46);
    }

    [Fact]
    public async Task Readings_older_than_a_day_are_not_current()
    {
        var circle = await SeedCircleAsync("elder-1", Caregiver);
        await SyncHeartRateAsync("elder-1", "hr-old", Now.AddHours(-30), 80, 81);
        await SyncOkAsync("elder-1", Body(sleep: [Session("sleep-old", Now.AddHours(-40), Now.AddHours(-33))]));

        var patient = await GetOkAsync($"/api/patients/{circle}", Caregiver);

        Assert.True(IsNull(patient, "heartRateBpm"));
        Assert.True(IsNull(patient, "sleepMinutes"));
    }

    [Fact]
    public async Task A_revoked_consent_hides_the_matching_fields()
    {
        var circle = await SeedCircleAsync("elder-1", Caregiver);
        await SyncHeartRateAsync("elder-1", "hr-1", Now.AddMinutes(-30), 70, 72);
        await SyncOkAsync("elder-1", Body(sleep: [Session("sleep-1", Now.AddHours(-9), Now.AddHours(-2))]));
        await RevokeConsentAsync("elder-1", HealthDataType.HeartRate);

        var patient = await GetOkAsync($"/api/patients/{circle}", Caregiver);

        Assert.True(IsNull(patient, "heartRateBpm"));
        Assert.Equal(420, patient.GetProperty("sleepMinutes").GetInt32());
    }
}
