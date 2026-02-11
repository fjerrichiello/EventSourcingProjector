using Common.Events;
using Common.Kafka;
using KafkaFlow;

namespace TransactionService.Producers;

public class EventStoredProducer(IMessageProducer<EventStoredProducer> _producer)
    : IEventStoredProducer
{
    public async Task PublishAsync(EventStored eventStored)
    {
        await _producer.ProduceAsync(KafkaConstants.Topics.EventStored, eventStored.AccountId,
            eventStored);
    }
}