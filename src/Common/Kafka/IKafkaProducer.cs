namespace Common.Kafka;

public interface IKafkaProducer<in T>
{
    Task ProduceAsync(string topic, string key, T message, CancellationToken ct = default);
}
