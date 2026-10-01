using Microsoft.Extensions.Options;
using Poc.Exchange.Callback.Models;
using Poc.Exchange.Callback.Options;
using Poc.Exchange.Callback.Services;

namespace Poc.Exchange.Callback.Endpoints;

public static class GraphEndpoints
{
    public static void MapGraphEndpoints(this WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
        app.MapGet("/notifications", () => Results.Redirect("/notifications.html"));

        app.MapMethods("/api/graph/notifications", ["GET", "POST"], HandleNotification)
            .WithName("GraphNotifications")
            .AllowAnonymous();

        app.MapGet("/api/notifications", (INotificationStore store) => store.ListRecent());

        app.MapPost("/api/subscriptions", CreateSubscription);
    }

    private static async Task<IResult> HandleNotification(
        HttpRequest request,
        IOptions<GraphOptions> options,
        INotificationStore store,
        IGraphCalendarService graph,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("GraphNotifications");

        if (request.Query.TryGetValue("validationToken", out var validationToken)
            && !string.IsNullOrWhiteSpace(validationToken))
        {
            logger.LogInformation("Responding to Graph subscription validation.");
            return Results.Text(validationToken.ToString(), "text/plain");
        }

        GraphNotificationPayload? payload;
        try
        {
            payload = await request.ReadFromJsonAsync<GraphNotificationPayload>(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Invalid Graph notification payload.");
            return Results.BadRequest();
        }

        if (payload?.Value is null || payload.Value.Count == 0)
        {
            return Results.Accepted();
        }

        foreach (var notification in payload.Value)
        {
            if (!string.Equals(notification.ClientState, options.Value.ClientState, StringComparison.Ordinal))
            {
                logger.LogWarning("Ignored notification with unexpected clientState.");
                continue;
            }

            var received = await graph.ToReceivedAsync(notification, cancellationToken);
            store.Add(received);
            logger.LogInformation(
                "Calendar {ChangeType} for event {EventId} subject {Subject}",
                received.ChangeType,
                received.EventId,
                received.Subject);
        }

        return Results.Accepted();
    }

    private static async Task<IResult> CreateSubscription(
        IGraphCalendarService graph,
        IOptions<GraphOptions> options,
        CancellationToken cancellationToken)
    {
        Microsoft.Graph.Models.Subscription created;
        try
        {
            created = await graph.CreateSubscriptionAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        return Results.Ok(new
        {
            created.Id,
            created.Resource,
            created.NotificationUrl,
            created.ExpirationDateTime,
            mailbox = options.Value.Mailbox
        });
    }
}
