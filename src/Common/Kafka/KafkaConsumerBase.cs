using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Common.Kafka;

public abstract class KafkaConsumerBase<T> : BackgroundService
{
    private readonly string _topic;
    private readonly string _groupId;
    private readonly string _bootstrapServers;
    private readonly ILogger _logger;

    protected KafkaConsumerBase(string topic, string groupId, string bootstrapServers, ILogger logger)
    {
        _topic = topic;
        _groupId = groupId;
        _bootstrapServers = bootstrapServers;
        _logger = logger;
    }

    protected abstract Task ProcessAsync(string key, T message, CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Yield to allow the host to finish starting up
        await Task.Yield();

        var config = new ConsumerConfig
        {
            
            BootstrapServers = _bootstrapServers,
            GroupId = _groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(_topic);

        _logger.LogInformation("Consumer started: topic={Topic}, group={GroupId}", _topic, _groupId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);

                if (result?.Message?.Value is null)
                    continue;

                var message = JsonSerializer.Deserialize<T>(result.Message.Value);
                if (message is null)
                {
                    _logger.LogWarning("Failed to deserialize message from {Topic} @ offset {Offset}",
                        _topic, result.Offset.Value);
                    continue;
                }

                await ProcessAsync(result.Message.Key, message, stoppingToken);

                consumer.Commit(result);
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Consume error on topic {Topic}", _topic);
                await Task.Delay(1000, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message from {Topic}", _topic);
                // Don't commit — at-least-once semantics. Delay before retry.
                await Task.Delay(1000, stoppingToken);
            }
        }

        consumer.Close();
        _logger.LogInformation("Consumer stopped: topic={Topic}, group={GroupId}", _topic, _groupId);
    }
}
