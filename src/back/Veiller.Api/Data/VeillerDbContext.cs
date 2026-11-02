using Microsoft.EntityFrameworkCore;
using Veiller.Api.Data.Entities;

namespace Veiller.Api.Data;

public sealed class VeillerDbContext(DbContextOptions<VeillerDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Circle> Circles => Set<Circle>();

    public DbSet<CircleMember> CircleMembers => Set<CircleMember>();

    public DbSet<Consent> Consents => Set<Consent>();

    public DbSet<InviteCode> InviteCodes => Set<InviteCode>();

    public DbSet<JoinRequest> JoinRequests => Set<JoinRequest>();

    public DbSet<HeartRateRecord> HeartRateRecords => Set<HeartRateRecord>();

    public DbSet<HeartRateSample> HeartRateSamples => Set<HeartRateSample>();

    public DbSet<StepsHour> StepsHourly => Set<StepsHour>();

    public DbSet<SleepSession> SleepSessions => Set<SleepSession>();

    public DbSet<SleepStage> SleepStages => Set<SleepStage>();

    public DbSet<HealthSyncStatus> HealthSyncStatuses => Set<HealthSyncStatus>();

    public DbSet<Alert> Alerts => Set<Alert>();

    public DbSet<AlertRule> AlertRules => Set<AlertRule>();

    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VeillerDbContext).Assembly);
}
