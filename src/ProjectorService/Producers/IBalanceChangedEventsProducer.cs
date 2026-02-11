using Common.Events;

namespace ProjectorService.Producers;

public interface IBalanceChangedEventsProducer
{
    Task PublishAsync(BalanceChanged balanceChanged);
}