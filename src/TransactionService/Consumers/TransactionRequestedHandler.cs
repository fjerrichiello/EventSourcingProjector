using Common.Events;
using KafkaFlow;
using TransactionService.Services;

namespace TransactionService.Consumers;

public class TransactionRequestedHandler(
    IServiceScopeFactory _scopeFactory,
    ILogger<TransactionRequestedHandler> _logger)
    : IMessageHandler<TransactionRequested>
{
    public async Task Handle(IMessageContext context, TransactionRequested message)
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<TransactionStorageService>();
        await service.ProcessTransactionAsync(message);
    }
}