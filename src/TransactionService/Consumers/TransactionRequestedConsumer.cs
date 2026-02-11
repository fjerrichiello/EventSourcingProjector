using Common.Events;
using Common.Kafka;
using TransactionService.Services;

namespace TransactionService.Consumers;

public class TransactionRequestedConsumer : KafkaConsumerBase<TransactionRequested>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public TransactionRequestedConsumer(
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        ILogger<TransactionRequestedConsumer> logger)
        : base(
            topic: "account-transactions",
            groupId: "transaction-service-group",
            bootstrapServers: configuration["Kafka:BootstrapServers"] ?? "localhost:9092",
            logger: logger)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ProcessAsync(string key, TransactionRequested message, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<TransactionStorageService>();
        await service.ProcessTransactionAsync(message, ct);
    }
}
