using Veiller.Api.Tests.Patients;

namespace Veiller.Api.Tests.Elder;

/// <summary>The watch "connected" window comes from configuration: here 10 minutes instead of the 6 hour default.</summary>
public class ElderHomeWindowTests() : PatientApiTestBase(new Dictionary<string, string?> { ["HealthSync:WatchConnectedWindow"] = "00:10:00" })
{
    [Fact]
    public async Task A_30_minute_old_signal_is_disconnected_with_a_10_minute_window()
    {
        await SeedCircleAsync(Elder);
        await SyncOkAsync(Elder, Body(watchLastSeenAt: Now.AddMinutes(-30)));

        var home = await GetOkAsync("/api/elder/home", Elder);

        Assert.False(home.GetProperty("watch").GetProperty("connected").GetBoolean());
    }

    [Fact]
    public async Task A_5_minute_old_signal_is_connected_with_a_10_minute_window()
    {
        await SeedCircleAsync(Elder);
        await SyncOkAsync(Elder, Body(watchLastSeenAt: Now.AddMinutes(-5)));

        var home = await GetOkAsync("/api/elder/home", Elder);

        Assert.True(home.GetProperty("watch").GetProperty("connected").GetBoolean());
    }
}
