using Common.Events;

namespace TransactionService.Producers;

public interface IEventStoredProducer
{
    Task PublishAsync(EventStored eventStored);
}