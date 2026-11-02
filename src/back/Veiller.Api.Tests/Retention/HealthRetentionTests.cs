using Microsoft.EntityFrameworkCore;
using Veiller.Api.Data.Entities;
using Veiller.Api.Retention;
using Veiller.Api.Tests.Infrastructure;
using AuthRole = Veiller.Api.Auth.Role;

namespace Veiller.Api.Tests.Retention;

/// <summary>The purge is called directly with a fixed clock: the background worker is disabled in tests.</summary>
public sealed class HealthRetentionTests : IDisposable
{
    private const string Elder = "elder-1";
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly VeillerApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task SeedAsync(DateTime heartRateEnd, DateTime stepsStart, DateTime sleepEnd)
    {
        using var _ = _factory.CreateDbScope(out var db);
        db.Users.Add(new User { Id = Elder, Email = "e@example.com", FullName = "Elder", Role = AuthRole.Elder, CreatedAt = Now });
        db.HeartRateRecords.Add(new HeartRateRecord
        {
            ElderId = Elder, HcRecordId = "hr", Origin = "app", Start = heartRateEnd.AddMinutes(-10), End = heartRateEnd, UpdatedAt = heartRateEnd,
            Samples = [new HeartRateSample { ElderId = Elder, HcRecordId = "hr", Time = heartRateEnd.AddMinutes(-5), Bpm = 70 }],
        });
        db.StepsHourly.Add(new StepsHour { ElderId = Elder, Start = stepsStart, End = stepsStart.AddHours(1), Count = 10, UpdatedAt = stepsStart });
        db.SleepSessions.Add(new SleepSession
        {
            ElderId = Elder, HcRecordId = "sl", Origin = "app", Start = sleepEnd.AddHours(-7), End = sleepEnd, UpdatedAt = sleepEnd,
            Stages = [new SleepStage { ElderId = Elder, HcRecordId = "sl", Ordinal = 0, Start = sleepEnd.AddHours(-7), End = sleepEnd, Stage = "LIGHT" }],
        });
        await db.SaveChangesAsync(Ct);
    }

    private async Task<RetentionResult> PurgeAsync(int days)
    {
        using var _ = _factory.CreateDbScope(out var db);
        return await HealthRetentionService.PurgeAsync(db, Now, days, Ct);
    }

    private async Task<List<int>> CountsAsync()
    {
        using var _ = _factory.CreateDbScope(out var db);
        return
        [
            await db.HeartRateRecords.CountAsync(Ct), await db.HeartRateSamples.CountAsync(Ct), await db.StepsHourly.CountAsync(Ct),
            await db.SleepSessions.CountAsync(Ct), await db.SleepStages.CountAsync(Ct),
        ];
    }

    [Fact]
    public async Task Data_older_than_the_limit_is_deleted_with_its_samples_and_stages()
    {
        var old = Now.UtcDateTime.AddDays(-400);
        await SeedAsync(old, old, old);

        var result = await PurgeAsync(365);

        Assert.Equal(new RetentionResult(1, 1, 1), result);
        Assert.Equal(new List<int> { 0, 0, 0, 0, 0 }, await CountsAsync());
    }

    [Fact]
    public async Task Recent_data_is_kept()
    {
        var recent = Now.UtcDateTime.AddDays(-10);
        await SeedAsync(recent, recent, recent);

        var result = await PurgeAsync(365);

        Assert.Equal(0, result.Total);
        Assert.Equal(new List<int> { 1, 1, 1, 1, 1 }, await CountsAsync());
    }

    [Fact]
    public async Task Each_type_is_judged_on_its_own_age()
    {
        await SeedAsync(heartRateEnd: Now.UtcDateTime.AddDays(-400), stepsStart: Now.UtcDateTime.AddDays(-1), sleepEnd: Now.UtcDateTime.AddDays(-1));

        var result = await PurgeAsync(365);

        Assert.Equal(new RetentionResult(1, 0, 0), result);
        Assert.Equal(new List<int> { 0, 0, 1, 1, 1 }, await CountsAsync());
    }

    [Fact]
    public async Task A_shorter_limit_deletes_more_of_the_same_data()
    {
        var tenDaysOld = Now.UtcDateTime.AddDays(-40);
        await SeedAsync(tenDaysOld, tenDaysOld, tenDaysOld);

        Assert.Equal(0, (await PurgeAsync(365)).Total);
        Assert.Equal(3, (await PurgeAsync(30)).Total);
    }

    [Fact]
    public async Task Purging_twice_is_harmless()
    {
        var old = Now.UtcDateTime.AddDays(-400);
        await SeedAsync(old, old, old);
        await PurgeAsync(365);

        Assert.Equal(0, (await PurgeAsync(365)).Total);
    }
}
