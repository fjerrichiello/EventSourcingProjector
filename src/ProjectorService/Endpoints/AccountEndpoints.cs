using ProjectorService.Services;

namespace ProjectorService.Endpoints;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        app.MapGet("/api/accounts/{accountId}/balance", async (
            string accountId,
            AccountQueryService queryService,
            CancellationToken ct) =>
        {
            var state = await queryService.GetBalanceAsync(accountId, ct);

            if (state is null)
                return Results.NotFound(new { error = $"No state found for account {accountId}" });

            return Results.Ok(new { accountId = state.AccountId, balance = state.CurrentBalance });
        });

        app.MapGet("/api/accounts/{accountId}/events", async (
            string accountId,
            AccountQueryService queryService,
            CancellationToken ct) =>
        {
            var events = await queryService.GetEventsAsync(accountId, ct);
            return Results.Ok(events);
        });

        app.MapGet("/health", () => Results.Ok("healthy"));
    }
}
