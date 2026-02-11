namespace Common.Events;

public record EventStored
{
    public required Guid EventId { get; init; }
    public required string AccountId { get; init; }
    public required long SequenceNum { get; init; }
    public required string EventType { get; init; }
    public required decimal Amount { get; init; }
    public string? Description { get; init; }
    public required Guid CorrelationId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
