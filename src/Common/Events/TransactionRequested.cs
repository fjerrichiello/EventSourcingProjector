namespace Common.Events;

public record TransactionRequested
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
    public required string AccountId { get; init; }
    public required string EventType { get; init; }
    public required decimal Amount { get; init; }
    public string? Description { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
