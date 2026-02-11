using Common.Events;
using KafkaFlow;
using ProjectorService.Services;

namespace ProjectorService.Consumers;

public class EventStoredHandler(IServiceScopeFactory _scopeFactory, ILogger<EventStoredHandler> _logger)
    : IMessageHandler<EventStored>
{
    public async Task Handle(IMessageContext context, EventStored message)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ProjectionService>();
            await service.ProjectEventAsync(message);
        }
        catch (SequenceGapException ex)
        {
            _logger.LogWarning(ex, "Sequence gap detected, will retry after delay");
            await Task.Delay(1000);
            throw; // Rethrow so KafkaConsumerBase doesn't commit the offset
        }
    }
}