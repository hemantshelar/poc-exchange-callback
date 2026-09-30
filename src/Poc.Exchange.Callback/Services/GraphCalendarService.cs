using Azure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Poc.Exchange.Callback.Models;
using Poc.Exchange.Callback.Options;

namespace Poc.Exchange.Callback.Services;

public sealed class GraphCalendarService : IGraphCalendarService
{
    private readonly GraphOptions _options;
    private readonly ILogger<GraphCalendarService> _logger;
    private readonly Lazy<GraphServiceClient> _client;

    public GraphCalendarService(IOptions<GraphOptions> options, ILogger<GraphCalendarService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _client = new Lazy<GraphServiceClient>(CreateClient);
    }

    public async Task<Event?> GetEventAsync(string eventId, CancellationToken cancellationToken)
    {
        return await _client.Value.Users[_options.Mailbox].Events[eventId].GetAsync(cancellationToken: cancellationToken);
    }

    public async Task<Subscription> CreateSubscriptionAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.NotificationUrl))
        {
            throw new InvalidOperationException("Graph:NotificationUrl is required to create a subscription.");
        }

        if (string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            throw new InvalidOperationException("Graph:ClientSecret is required to call Microsoft Graph.");
        }

        var subscription = new Subscription
        {
            ChangeType = "created,updated,deleted",
            NotificationUrl = _options.NotificationUrl,
            Resource = $"/users/{_options.Mailbox}/events",
            ExpirationDateTime = DateTimeOffset.UtcNow.AddHours(70),
            ClientState = _options.ClientState
        };

        var created = await _client.Value.Subscriptions.PostAsync(subscription, cancellationToken: cancellationToken);
        return created ?? throw new InvalidOperationException("Graph did not return a subscription.");
    }

    public async Task<ReceivedNotification> ToReceivedAsync(GraphNotification notification, CancellationToken cancellationToken)
    {
        var eventId = notification.ResourceData?.Id ?? "(unknown)";
        string? subject = null;
        string? organizer = null;
        string? start = null;
        string? end = null;
        string? detail = null;

        if (!string.IsNullOrWhiteSpace(notification.ResourceData?.Id)
            && !string.Equals(notification.ChangeType, "deleted", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var calendarEvent = await GetEventAsync(notification.ResourceData.Id, cancellationToken);
                subject = calendarEvent?.Subject;
                organizer = calendarEvent?.Organizer?.EmailAddress?.Address;
                start = calendarEvent?.Start?.DateTime;
                end = calendarEvent?.End?.DateTime;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load event {EventId} from Graph.", eventId);
                detail = ex.Message;
            }
        }

        return new ReceivedNotification
        {
            ReceivedAtUtc = DateTimeOffset.UtcNow,
            ChangeType = notification.ChangeType ?? "unknown",
            EventId = eventId,
            Subject = subject,
            Organizer = organizer,
            Start = start,
            End = end,
            Detail = detail
        };
    }

    private GraphServiceClient CreateClient()
    {
        var credential = new ClientSecretCredential(
            _options.TenantId,
            _options.ClientId,
            _options.ClientSecret);

        return new GraphServiceClient(credential, ["https://graph.microsoft.com/.default"]);
    }
}
