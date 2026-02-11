using ConsumerApi.Services;

namespace ConsumerApi.Endpoints;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this WebApplication app)
    {
        app.MapGet("/api/accounts/{accountId}/notifications", async (
            string accountId,
            NotificationService notificationService,
            CancellationToken ct) =>
        {
            var notifications = await notificationService.GetNotificationsAsync(accountId, ct);
            return Results.Ok(notifications);
        });

        app.MapGet("/health", () => Results.Ok("healthy"));
    }
}
