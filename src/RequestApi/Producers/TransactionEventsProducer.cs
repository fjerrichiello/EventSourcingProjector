using Common.Events;
using Common.Kafka;
using KafkaFlow;

namespace RequestApi.Producers;

public class TransactionEventsProducer(IMessageProducer<TransactionEventsProducer> _producer)
    : ITransactionEventsProducer
{
    public async Task PublishAsync(TransactionRequested transactionRequested)
    {
        await _producer.ProduceAsync(KafkaConstants.Topics.AccountTransactions, transactionRequested.AccountId,
            transactionRequested);
    }
}