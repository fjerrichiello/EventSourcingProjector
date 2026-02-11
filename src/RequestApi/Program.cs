using Common.Events;
using Common.Kafka;
using Confluent.Kafka;
using KafkaFlow;
using KafkaFlow.Serializer;
using RequestApi.Endpoints;
using RequestApi.Producers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var bootstrapServers = builder.Configuration["Kafka:BootstrapServers"] ?? "localhost:9092";

builder.Services.AddKafka(kafka => kafka.AddCluster(cluster => cluster.WithBrokers([bootstrapServers])
    .AddProducer<TransactionEventsProducer>(producer =>
        producer.AddMiddlewares(middlewares => middlewares.AddSerializer<JsonCoreSerializer>()))));

builder.Services.AddSingleton<ITransactionEventsProducer, TransactionEventsProducer>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapTransactionEndpoints();

app.Run();