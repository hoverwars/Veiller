using System.Net;
using System.Text.Json;
using Veiller.Api.Tests.Patients;

namespace Veiller.Api.Tests.Elder;

public class ElderHomeTests : PatientApiTestBase
{
    private const string Url = "/api/elder/home";

    // ---- Authorization ----

    [Fact]
    public async Task Anonymous_gets_401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync(Url, null)).StatusCode);
    }

    [Fact]
    public async Task A_caregiver_gets_403()
    {
        await SeedCircleAsync(Elder, Caregiver);

        Assert.Equal(HttpStatusCode.Forbidden, (await GetAsync(Url, Caregiver)).StatusCode);
    }

    [Fact]
    public async Task An_unknown_user_gets_403()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await GetAsync(Url, "ghost")).StatusCode);
    }

    // ---- Last send and watch ----

    [Fact]
    public async Task Never_synced_gives_null_minutes_and_a_disconnected_watch()
    {
        await SeedCircleAsync(Elder);

        var home = await GetOkAsync(Url, Elder);

        Assert.True(IsNull(home, "lastSendMinutesAgo"));
        Assert.False(home.GetProperty("watch").GetProperty("connected").GetBoolean());
        Assert.True(IsNull(home.GetProperty("watch"), "batteryPercent"));
        Assert.Empty(home.GetProperty("caregivers").EnumerateArray());
    }

    [Fact]
    public async Task A_fresh_sync_with_a_recent_watch_signal_is_connected()
    {
        await SeedCircleAsync(Elder);
        await SyncOkAsync(Elder, Body(watchLastSeenAt: Now.AddMinutes(-30)));

        var home = await GetOkAsync(Url, Elder);

        Assert.InRange(home.GetProperty("lastSendMinutesAgo").GetInt32(), 0, 1);
        Assert.True(home.GetProperty("watch").GetProperty("connected").GetBoolean());
    }

    [Fact]
    public async Task A_watch_signal_older_than_the_window_is_disconnected_but_the_upload_still_counts()
    {
        await SeedCircleAsync(Elder);
        await SyncOkAsync(Elder, Body(watchLastSeenAt: Now.AddHours(-7)));

        var home = await GetOkAsync(Url, Elder);

        Assert.False(home.GetProperty("watch").GetProperty("connected").GetBoolean());
        Assert.InRange(home.GetProperty("lastSendMinutesAgo").GetInt32(), 0, 1);
    }

    [Fact]
    public async Task A_sync_without_watch_signal_is_disconnected()
    {
        await SeedCircleAsync(Elder);
        await SyncOkAsync(Elder, Body());

        var home = await GetOkAsync(Url, Elder);

        Assert.False(home.GetProperty("watch").GetProperty("connected").GetBoolean());
        Assert.False(IsNull(home, "lastSendMinutesAgo"));
    }

    [Fact]
    public async Task The_connected_window_is_configurable()
    {
        using var narrow = new NarrowWindowTests();
        await narrow.RunAsync();
    }

    // ---- Caregivers ----

    [Fact]
    public async Task Caregivers_exclude_the_elder_and_are_ordered_by_join_date()
    {
        await SeedCircleAsync(Elder, "caregiver-1", "caregiver-2");

        var home = await GetOkAsync(Url, Elder);
        var caregivers = home.GetProperty("caregivers").EnumerateArray().ToList();

        Assert.Equal(2, caregivers.Count);
        Assert.DoesNotContain(caregivers, c => c.GetProperty("userId").GetString() == Elder);
        Assert.All(caregivers, c => Assert.Equal("Caregiver", c.GetProperty("role").GetString()));
        Assert.Equal("Name caregiver-1", caregivers[0].GetProperty("fullName").GetString());
    }

    [Fact]
    public async Task Another_circle_is_never_visible()
    {
        await SeedCircleAsync(Elder, "caregiver-1");
        await SeedCircleAsync("elder-2", "caregiver-2");

        var home = await GetOkAsync(Url, Elder);

        var ids = home.GetProperty("caregivers").EnumerateArray().Select(c => c.GetProperty("userId").GetString());
        Assert.Equal(["caregiver-1"], ids);
    }

    [Fact]
    public async Task Another_elders_sync_does_not_leak_into_the_home()
    {
        await SeedCircleAsync(Elder);
        await SeedCircleAsync("elder-2");
        await SyncOkAsync("elder-2", Body(watchLastSeenAt: Now.AddMinutes(-1)));

        var home = await GetOkAsync(Url, Elder);

        Assert.True(IsNull(home, "lastSendMinutesAgo"));
        Assert.False(home.GetProperty("watch").GetProperty("connected").GetBoolean());
    }

    [Fact]
    public async Task An_elder_without_circle_gets_an_empty_list()
    {
        await SeedUserAsync(Elder);

        var home = await GetOkAsync(Url, Elder);

        Assert.Empty(home.GetProperty("caregivers").EnumerateArray());
    }

    /// <summary>Same scenario with a 10 minute window: a 30 minute old signal is no longer "connected".</summary>
    private sealed class NarrowWindowTests : PatientApiTestBase
    {
        public NarrowWindowTests() : base(new Dictionary<string, string?> { ["HealthSync:WatchConnectedWindow"] = "00:10:00" }) { }

        public async Task RunAsync()
        {
            await SeedCircleAsync(Elder);
            await SyncOkAsync(Elder, Body(watchLastSeenAt: Now.AddMinutes(-30)));

            var home = await GetOkAsync(Url, Elder);

            Assert.False(home.GetProperty("watch").GetProperty("connected").GetBoolean());
        }
    }
}
