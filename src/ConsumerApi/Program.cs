using ConsumerApi.Consumers;
using ConsumerApi.Endpoints;
using ConsumerApi.Persistence;
using ConsumerApi.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("EventStore")
    ?? "Host=localhost;Database=eventstore;Username=postgres;Password=postgres";

builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<NotificationService>();
builder.Services.AddHostedService<BalanceChangedConsumer>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapNotificationEndpoints();

app.Run();
