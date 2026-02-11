using Common.Events;
using Common.Kafka;
using Confluent.Kafka;
using KafkaFlow;
using KafkaFlow.Serializer;
using Microsoft.EntityFrameworkCore;
using TransactionService.Consumers;
using TransactionService.Persistence;
using TransactionService.Producers;
using TransactionService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("EventStore")
                       ?? "Host=localhost;Database=eventstore;Username=postgres;Password=postgres";
var bootstrapServers = builder.Configuration["Kafka:BootstrapServers"] ?? "localhost:9092";

builder.Services.AddDbContext<EventStoreDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddKafka(kafka => kafka.AddCluster(cluster => cluster.WithBrokers([bootstrapServers])
    .AddProducer<EventStoredProducer>(producer =>
        producer.AddMiddlewares(middlewares => middlewares.AddSerializer<JsonCoreSerializer>()))
    .AddConsumer(consumer => consumer.Topic(KafkaConstants.Topics.AccountTransactions)
        .WithGroupId(KafkaConstants.GroupId.TransactionServiceGroup).WithBufferSize(100).WithWorkersCount(10)
        .AddMiddlewares(middlewares => middlewares.AddTypedHandlers(handlers =>
                handlers.WithHandlerLifetime(InstanceLifetime.Singleton).AddHandler<TransactionRequestedHandler>())
            .AddDeserializer<JsonCoreDeserializer>()))));

builder.Services.AddSingleton<IEventStoredProducer, EventStoredProducer>();

builder.Services.AddScoped<TransactionStorageService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok("healthy"));

app.Run();