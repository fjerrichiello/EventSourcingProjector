using Common.Events;
using Common.Kafka;
using Confluent.Kafka;
using KafkaFlow;
using KafkaFlow.Serializer;
using Microsoft.EntityFrameworkCore;
using ProjectorService.Consumers;
using ProjectorService.Endpoints;
using ProjectorService.Persistence;
using ProjectorService.Producers;
using ProjectorService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("EventStore")
                       ?? "Host=localhost;Database=eventstore;Username=postgres;Password=postgres";
var bootstrapServers = builder.Configuration["Kafka:BootstrapServers"] ?? "localhost:9092";


builder.Services.AddKafka(kafka => kafka.AddCluster(cluster => cluster.WithBrokers([bootstrapServers])
    .AddProducer<BalanceChangedEventsProducer>(producer =>
        producer.AddMiddlewares(middlewares => middlewares.AddSerializer<JsonCoreSerializer>())
    )
    .AddConsumer(consumer => consumer.Topic(KafkaConstants.Topics.EventStored)
        .WithGroupId(KafkaConstants.GroupId.ProjectorServiceGroup).WithBufferSize(100).WithWorkersCount(10)
        .AddMiddlewares(middlewares => middlewares.AddTypedHandlers(handlers =>
                handlers.WithHandlerLifetime(InstanceLifetime.Singleton).AddHandler<EventStoredHandler>())
            .AddDeserializer<JsonCoreDeserializer>()))));

builder.Services.AddDbContext<ProjectorDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddSingleton<IBalanceChangedEventsProducer, BalanceChangedEventsProducer>();
builder.Services.AddSingleton<IBalanceChangedEventsProducer>();
builder.Services.AddScoped<ProjectionService>();
builder.Services.AddScoped<AccountQueryService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapAccountEndpoints();

app.Run();