using Common.Events;

namespace RequestApi.Producers;

public interface ITransactionEventsProducer
{
    Task PublishAsync(TransactionRequested transactionRequested);
}