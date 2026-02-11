using Common.Events;
using Common.Kafka;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using ProjectorService.Consumers;
using ProjectorService.Endpoints;
using ProjectorService.Persistence;
using ProjectorService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("EventStore")
    ?? "Host=localhost;Database=eventstore;Username=postgres;Password=postgres";
var bootstrapServers = builder.Configuration["Kafka:BootstrapServers"] ?? "localhost:9092";

builder.Services.AddDbContext<ProjectorDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddSingleton<IProducer<string, string>>(_ =>
    new ProducerBuilder<string, string>(new ProducerConfig
    {
        BootstrapServers = bootstrapServers
    }).Build());

builder.Services.AddSingleton<IKafkaProducer<BalanceChanged>, KafkaProducerService<BalanceChanged>>();
builder.Services.AddScoped<ProjectionService>();
builder.Services.AddScoped<AccountQueryService>();
builder.Services.AddHostedService<EventStoredConsumer>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapAccountEndpoints();

app.Run();
