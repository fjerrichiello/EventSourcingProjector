using Common.Events;
using Common.Kafka;
using Confluent.Kafka;
using RequestApi.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var bootstrapServers = builder.Configuration["Kafka:BootstrapServers"] ?? "localhost:9092";

builder.Services.AddSingleton<IProducer<string, string>>(_ =>
    new ProducerBuilder<string, string>(new ProducerConfig
    {
        BootstrapServers = bootstrapServers
    }).Build());

builder.Services.AddSingleton<IKafkaProducer<TransactionRequested>, KafkaProducerService<TransactionRequested>>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapTransactionEndpoints();

app.Run();
