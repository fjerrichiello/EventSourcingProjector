using DbMigrator.Entities;
using Microsoft.EntityFrameworkCore;

namespace DbMigrator;

public class MigrationDbContext : DbContext
{
    public MigrationDbContext(DbContextOptions<MigrationDbContext> options) : base(options)
    {
    }

    public DbSet<EventStoreEntry> Events => Set<EventStoreEntry>();
    public DbSet<ProjectedState> ProjectedStates => Set<ProjectedState>();
    public DbSet<BalanceEventLog> BalanceEventLogs => Set<BalanceEventLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EventStoreEntry>(entity =>
        {
            entity.HasIndex(e => new { e.AccountId, e.SequenceNum }).IsUnique();
            entity.HasIndex(e => new { e.AccountId, e.SequenceNum });
        });

        modelBuilder.Entity<BalanceEventLog>(entity =>
        {
            entity.HasIndex(e => new { e.AccountId, e.ReceivedAt })
                .IsDescending(false, true);
        });
    }
}
