using Common.Events;
using Common.Kafka;
using RequestApi.Models;

namespace RequestApi.Endpoints;

public static class TransactionEndpoints
{
    private const string Topic = "account-transactions";

    public static void MapTransactionEndpoints(this WebApplication app)
    {
        app.MapPost("/api/accounts/{accountId}/transactions", async (
            string accountId,
            CreateTransactionRequest request,
            IKafkaProducer<TransactionRequested> producer) =>
        {
            if (string.IsNullOrWhiteSpace(accountId))
                return Results.BadRequest(new { error = "accountId is required" });

            if (request.Amount <= 0)
                return Results.BadRequest(new { error = "Amount must be greater than zero" });

            var eventType = request.Type?.ToLowerInvariant();
            if (eventType is not ("credit" or "debit"))
                return Results.BadRequest(new { error = "Type must be 'credit' or 'debit'" });

            var transactionEvent = new TransactionRequested
            {
                AccountId = accountId,
                EventType = eventType,
                Amount = request.Amount,
                Description = request.Description
            };

            await producer.ProduceAsync(Topic, accountId, transactionEvent);

            return Results.Accepted(value: new { correlationId = transactionEvent.CorrelationId });
        });

        app.MapGet("/health", () => Results.Ok("healthy"));
    }
}
