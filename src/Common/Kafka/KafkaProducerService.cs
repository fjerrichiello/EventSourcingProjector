using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace Common.Kafka;

public class KafkaProducerService<T> : IKafkaProducer<T>
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaProducerService<T>> _logger;

    public KafkaProducerService(IProducer<string, string> producer, ILogger<KafkaProducerService<T>> logger)
    {
        _producer = producer;
        _logger = logger;
    }

    public async Task ProduceAsync(string topic, string key, T message, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(message);

        try
        {
            var result = await _producer.ProduceAsync(topic, new Message<string, string>
            {
                Key = key,
                Value = json
            }, ct);

            _logger.LogDebug("Produced message to {Topic} [{Partition}] @ offset {Offset}",
                result.Topic, result.Partition.Value, result.Offset.Value);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex, "Failed to produce message to {Topic} with key {Key}", topic, key);
            throw;
        }
    }
}
