using Common.Events;
using Common.Kafka;
using KafkaFlow;

namespace ProjectorService.Producers;

public class BalanceChangedEventsProducer(IMessageProducer<BalanceChangedEventsProducer> _producer)
    : IBalanceChangedEventsProducer
{
    public async Task PublishAsync(BalanceChanged balanceChanged)
    {
        await _producer.ProduceAsync(KafkaConstants.Topics.AccountBalanceEvents, balanceChanged.AccountId,
            balanceChanged);
    }
}