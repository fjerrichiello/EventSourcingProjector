using DbMigrator.Entities;
using Microsoft.EntityFrameworkCore;

namespace TransactionService.Persistence;

public class EventStoreDbContext : DbContext
{
    public EventStoreDbContext(DbContextOptions<EventStoreDbContext> options) : base(options)
    {
    }

    public DbSet<EventStoreEntry> Events => Set<EventStoreEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EventStoreEntry>(entity =>
        {
            entity.HasIndex(e => new { e.AccountId, e.SequenceNum }).IsUnique();
        });
    }
}
