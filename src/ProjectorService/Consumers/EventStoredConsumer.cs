using Common.Events;
using Common.Kafka;
using ProjectorService.Services;

namespace ProjectorService.Consumers;

public class EventStoredConsumer : KafkaConsumerBase<EventStored>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EventStoredConsumer> _logger;

    public EventStoredConsumer(
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        ILogger<EventStoredConsumer> logger)
        : base(
            topic: "event-stored",
            groupId: "projector-service-group",
            bootstrapServers: configuration["Kafka:BootstrapServers"] ?? "localhost:9092",
            logger: logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ProcessAsync(string key, EventStored message, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ProjectionService>();
            await service.ProjectEventAsync(message, ct);
        }
        catch (SequenceGapException ex)
        {
            _logger.LogWarning(ex, "Sequence gap detected, will retry after delay");
            await Task.Delay(1000, ct);
            throw; // Rethrow so KafkaConsumerBase doesn't commit the offset
        }
    }
}
