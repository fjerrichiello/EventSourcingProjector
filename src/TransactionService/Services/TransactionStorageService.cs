using Common.Events;
using Common.Kafka;
using DbMigrator.Entities;
using Microsoft.EntityFrameworkCore;
using TransactionService.Persistence;

namespace TransactionService.Services;

public class TransactionStorageService
{
    private readonly EventStoreDbContext _dbContext;
    private readonly IKafkaProducer<EventStored> _producer;
    private readonly ILogger<TransactionStorageService> _logger;
    private const string Topic = "event-stored";

    public TransactionStorageService(
        EventStoreDbContext dbContext,
        IKafkaProducer<EventStored> producer,
        ILogger<TransactionStorageService> logger)
    {
        _dbContext = dbContext;
        _producer = producer;
        _logger = logger;
    }

    public async Task ProcessTransactionAsync(TransactionRequested msg, CancellationToken ct)
    {
        // Negate amount for debits
        var amount = msg.EventType == "debit" ? -msg.Amount : msg.Amount;

        // Assign sequence number within a transaction
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, ct);

        try
        {
            var maxSequence = await _dbContext.Events
                .Where(e => e.AccountId == msg.AccountId)
                .MaxAsync(e => (long?)e.SequenceNum, ct) ?? 0;

            var nextSequence = maxSequence + 1;

            var entry = new EventStoreEntry
            {
                Id = Guid.NewGuid(),
                AccountId = msg.AccountId,
                SequenceNum = nextSequence,
                EventType = msg.EventType,
                Amount = amount,
                Description = msg.Description,
                CorrelationId = msg.CorrelationId,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.Events.Add(entry);
            await _dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "Stored event for account {AccountId}: seq={Seq}, type={Type}, amount={Amount}",
                entry.AccountId, entry.SequenceNum, entry.EventType, entry.Amount);

            // Publish EventStored to Kafka
            var eventStored = new EventStored
            {
                EventId = entry.Id,
                AccountId = entry.AccountId,
                SequenceNum = entry.SequenceNum,
                EventType = entry.EventType,
                Amount = entry.Amount,
                Description = entry.Description,
                CorrelationId = entry.CorrelationId,
                CreatedAt = entry.CreatedAt
            };

            await _producer.ProduceAsync(Topic, entry.AccountId, eventStored, ct);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            await transaction.RollbackAsync(ct);
            _logger.LogWarning(
                "Sequence conflict for account {AccountId}, retrying...", msg.AccountId);
            throw; // Let the consumer retry
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        return ex.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true
            || ex.InnerException?.Message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase) == true;
    }
}
