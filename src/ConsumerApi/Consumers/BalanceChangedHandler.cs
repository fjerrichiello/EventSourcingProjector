using Common.Events;
using ConsumerApi.Services;
using KafkaFlow;

namespace ConsumerApi.Consumers;

public class BalanceChangedHandler(IServiceScopeFactory _scopeFactory, ILogger<BalanceChangedHandler> _logger)
    : IMessageHandler<BalanceChanged>
{
    public async Task Handle(IMessageContext context, BalanceChanged message)
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<NotificationService>();
        await service.ProcessBalanceChangedAsync(message);
    }
}