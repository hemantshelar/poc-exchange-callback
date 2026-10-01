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
        app.MapGet("/subscriptions", () => Results.Redirect("/subscriptions.html"));

        app.MapMethods("/api/graph/notifications", ["GET", "POST"], HandleNotification)
            .WithName("GraphNotifications")
            .AllowAnonymous();

        app.MapGet("/api/notifications", (INotificationStore store) => store.ListRecent());

        app.MapGet("/api/settings", (IOptions<GraphOptions> options) => Results.Ok(new
        {
            defaultMailbox = options.Value.Mailbox,
            notificationUrl = options.Value.NotificationUrl
        }));

        app.MapGet("/api/subscriptions", ListSubscriptions);
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

    private static async Task<IResult> ListSubscriptions(
        IGraphCalendarService graph,
        CancellationToken cancellationToken)
    {
        try
        {
            var items = await graph.ListSubscriptionsAsync(cancellationToken);
            return Results.Ok(items.Select(item => ToResult(item)).ToArray());
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> CreateSubscription(
        SubscriptionRequest? request,
        IGraphCalendarService graph,
        IOptions<GraphOptions> options,
        CancellationToken cancellationToken)
    {
        var mailbox = string.IsNullOrWhiteSpace(request?.Mailbox)
            ? options.Value.Mailbox
            : request.Mailbox.Trim();

        Microsoft.Graph.Models.Subscription created;
        try
        {
            created = await graph.CreateSubscriptionAsync(mailbox, cancellationToken);
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        return Results.Ok(ToResult(created, mailbox));
    }

    private static SubscriptionResult ToResult(Microsoft.Graph.Models.Subscription created, string? mailbox = null)
    {
        return new SubscriptionResult
        {
            Id = created.Id,
            Resource = created.Resource,
            NotificationUrl = created.NotificationUrl,
            ExpirationDateTime = created.ExpirationDateTime,
            ChangeType = created.ChangeType,
            ClientState = created.ClientState,
            Mailbox = mailbox ?? ExtractMailbox(created.Resource)
        };
    }

    private static string? ExtractMailbox(string? resource)
    {
        if (string.IsNullOrWhiteSpace(resource))
        {
            return null;
        }

        const string prefix = "/users/";
        const string suffix = "/events";
        var start = resource.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return resource;
        }

        start += prefix.Length;
        var end = resource.IndexOf(suffix, start, StringComparison.OrdinalIgnoreCase);
        return end < 0 ? resource[start..] : resource[start..end];
    }
}
