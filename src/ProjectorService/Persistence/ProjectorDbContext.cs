using DbMigrator.Entities;
using Microsoft.EntityFrameworkCore;

namespace ProjectorService.Persistence;

public class ProjectorDbContext : DbContext
{
    public ProjectorDbContext(DbContextOptions<ProjectorDbContext> options) : base(options)
    {
    }

    public DbSet<ProjectedState> ProjectedStates => Set<ProjectedState>();
    public DbSet<EventStoreEntry> Events => Set<EventStoreEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EventStoreEntry>(entity =>
        {
            entity.HasIndex(e => new { e.AccountId, e.SequenceNum }).IsUnique();
        });
    }
}
