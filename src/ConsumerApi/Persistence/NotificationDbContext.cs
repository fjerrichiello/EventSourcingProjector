using DbMigrator.Entities;
using Microsoft.EntityFrameworkCore;

namespace ConsumerApi.Persistence;

public class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options)
    {
    }

    public DbSet<BalanceEventLog> BalanceEventLogs => Set<BalanceEventLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BalanceEventLog>(entity =>
        {
            entity.HasIndex(e => new { e.AccountId, e.ReceivedAt })
                .IsDescending(false, true);
        });
    }
}
