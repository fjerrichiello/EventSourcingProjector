using Common.Events;
using Common.Kafka;
using ConsumerApi.Services;

namespace ConsumerApi.Consumers;

public class BalanceChangedConsumer : KafkaConsumerBase<BalanceChanged>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public BalanceChangedConsumer(
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        ILogger<BalanceChangedConsumer> logger)
        : base(
            topic: "account-balance-events",
            groupId: "notification-consumer-group",
            bootstrapServers: configuration["Kafka:BootstrapServers"] ?? "localhost:9092",
            logger: logger)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ProcessAsync(string key, BalanceChanged message, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<NotificationService>();
        await service.ProcessBalanceChangedAsync(message, ct);
    }
}
