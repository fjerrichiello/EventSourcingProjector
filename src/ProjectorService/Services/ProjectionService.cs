using Common.Events;
using Common.Kafka;
using DbMigrator.Entities;
using Microsoft.EntityFrameworkCore;
using ProjectorService.Persistence;

namespace ProjectorService.Services;

public class ProjectionService
{
    private readonly ProjectorDbContext _dbContext;
    private readonly IKafkaProducer<BalanceChanged> _producer;
    private readonly ILogger<ProjectionService> _logger;
    private const string Topic = "account-balance-events";

    public ProjectionService(
        ProjectorDbContext dbContext,
        IKafkaProducer<BalanceChanged> producer,
        ILogger<ProjectionService> logger)
    {
        _dbContext = dbContext;
        _producer = producer;
        _logger = logger;
    }

    public async Task ProjectEventAsync(EventStored evt, CancellationToken ct)
    {
        var state = await _dbContext.ProjectedStates
            .FindAsync([evt.AccountId], ct);

        if (state is null)
        {
            state = new ProjectedState
            {
                AccountId = evt.AccountId,
                CurrentBalance = 0,
                LastProcessedSequence = 0,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _dbContext.ProjectedStates.Add(state);
        }

        // Idempotency: skip already-processed events
        if (evt.SequenceNum <= state.LastProcessedSequence)
        {
            _logger.LogDebug(
                "Skipping already-processed event for {AccountId}: seq={Seq}, last_processed={LastProcessed}",
                evt.AccountId, evt.SequenceNum, state.LastProcessedSequence);
            return;
        }

        // Gap check: don't skip ahead
        if (evt.SequenceNum > state.LastProcessedSequence + 1)
        {
            throw new SequenceGapException(
                evt.AccountId,
                state.LastProcessedSequence + 1,
                evt.SequenceNum);
        }

        var balanceBefore = state.CurrentBalance;
        var balanceAfter = balanceBefore + evt.Amount;

        state.CurrentBalance = balanceAfter;
        state.LastProcessedSequence = evt.SequenceNum;
        state.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Projected event for {AccountId}: seq={Seq}, balance {Before} -> {After}",
            evt.AccountId, evt.SequenceNum, balanceBefore, balanceAfter);

        // Publish BalanceChanged after successful DB commit
        var balanceChanged = new BalanceChanged
        {
            EventId = evt.EventId,
            AccountId = evt.AccountId,
            SequenceNum = evt.SequenceNum,
            EventType = evt.EventType,
            Amount = evt.Amount,
            BalanceBefore = balanceBefore,
            BalanceAfter = balanceAfter,
            CorrelationId = evt.CorrelationId,
            Timestamp = DateTimeOffset.UtcNow
        };

        await _producer.ProduceAsync(Topic, evt.AccountId, balanceChanged, ct);
    }
}
