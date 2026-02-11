using Common.Events;
using Common.Kafka;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using TransactionService.Consumers;
using TransactionService.Persistence;
using TransactionService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("EventStore")
    ?? "Host=localhost;Database=eventstore;Username=postgres;Password=postgres";
var bootstrapServers = builder.Configuration["Kafka:BootstrapServers"] ?? "localhost:9092";

builder.Services.AddDbContext<EventStoreDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddSingleton<IProducer<string, string>>(_ =>
    new ProducerBuilder<string, string>(new ProducerConfig
    {
        BootstrapServers = bootstrapServers
    }).Build());

builder.Services.AddSingleton<IKafkaProducer<EventStored>, KafkaProducerService<EventStored>>();
builder.Services.AddScoped<TransactionStorageService>();
builder.Services.AddHostedService<TransactionRequestedConsumer>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok("healthy"));

app.Run();
