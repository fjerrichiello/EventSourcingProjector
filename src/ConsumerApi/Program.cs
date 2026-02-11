using Common.Kafka;
using ConsumerApi.Consumers;
using ConsumerApi.Endpoints;
using ConsumerApi.Persistence;
using ConsumerApi.Services;
using KafkaFlow;
using KafkaFlow.Serializer;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("EventStore")
                       ?? "Host=localhost;Database=eventstore;Username=postgres;Password=postgres";

var bootstrapServers = builder.Configuration["Kafka:BootstrapServers"] ?? "localhost:9092";


builder.Services.AddKafka(kafka => kafka.AddCluster(cluster => cluster.WithBrokers([bootstrapServers])
    .AddConsumer(consumer => consumer.Topic(KafkaConstants.Topics.AccountBalanceEvents)
        .WithGroupId(KafkaConstants.GroupId.NotificationConsumerGroup).WithBufferSize(100).WithWorkersCount(10)
        .AddMiddlewares(middlewares => middlewares.AddTypedHandlers(handlers =>
                handlers.WithHandlerLifetime(InstanceLifetime.Singleton).AddHandler<BalanceChangedHandler>())
            .AddDeserializer<JsonCoreDeserializer>()))));


builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<NotificationService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapNotificationEndpoints();

app.Run();