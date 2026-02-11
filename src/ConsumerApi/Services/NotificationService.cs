using Common.Events;
using ConsumerApi.Persistence;
using DbMigrator.Entities;
using Microsoft.EntityFrameworkCore;

namespace ConsumerApi.Services;

public class NotificationService
{
    private readonly NotificationDbContext _dbContext;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(NotificationDbContext dbContext, ILogger<NotificationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task ProcessBalanceChangedAsync(BalanceChanged evt)
    {
        var entry = new BalanceEventLog
        {
            Id = Guid.NewGuid(),
            AccountId = evt.AccountId,
            EventType = evt.EventType,
            Amount = evt.Amount,
            BalanceBefore = evt.BalanceBefore,
            BalanceAfter = evt.BalanceAfter,
            CorrelationId = evt.CorrelationId,
            ReceivedAt = DateTimeOffset.UtcNow
        };

        _dbContext.BalanceEventLogs.Add(entry);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Notification: account {AccountId} {EventType} {Amount:C}, balance {Before:C} -> {After:C}",
            evt.AccountId, evt.EventType, evt.Amount, evt.BalanceBefore, evt.BalanceAfter);
    }

    public async Task<List<BalanceEventLog>> GetNotificationsAsync(string accountId, CancellationToken ct)
    {
        return await _dbContext.BalanceEventLogs
            .Where(e => e.AccountId == accountId)
            .OrderByDescending(e => e.ReceivedAt)
            .ToListAsync(ct);
    }
}
