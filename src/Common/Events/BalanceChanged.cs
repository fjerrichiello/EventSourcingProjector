namespace Common.Events;

public record BalanceChanged
{
    public required Guid EventId { get; init; }
    public required string AccountId { get; init; }
    public required long SequenceNum { get; init; }
    public required string EventType { get; init; }
    public required decimal Amount { get; init; }
    public required decimal BalanceBefore { get; init; }
    public required decimal BalanceAfter { get; init; }
    public required Guid CorrelationId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
}
