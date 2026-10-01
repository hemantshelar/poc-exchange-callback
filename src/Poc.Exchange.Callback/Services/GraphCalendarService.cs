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

    public async Task<Event?> GetEventAsync(string mailbox, string eventId, CancellationToken cancellationToken)
    {
        return await _client.Value.Users[mailbox].Events[eventId].GetAsync(cancellationToken: cancellationToken);
    }

    public async Task<Subscription> CreateSubscriptionAsync(string mailbox, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.NotificationUrl))
        {
            throw new InvalidOperationException("Graph:NotificationUrl is required to create a subscription.");
        }

        if (string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            throw new InvalidOperationException("Graph:ClientSecret is required to call Microsoft Graph.");
        }

        if (string.IsNullOrWhiteSpace(mailbox))
        {
            throw new InvalidOperationException("Mailbox is required.");
        }

        var subscription = new Subscription
        {
            ChangeType = "created,updated,deleted",
            NotificationUrl = _options.NotificationUrl,
            Resource = $"/users/{mailbox.Trim()}/events",
            ExpirationDateTime = DateTimeOffset.UtcNow.AddHours(70),
            ClientState = _options.ClientState
        };

        var created = await _client.Value.Subscriptions.PostAsync(subscription, cancellationToken: cancellationToken);
        return created ?? throw new InvalidOperationException("Graph did not return a subscription.");
    }

    public async Task<IReadOnlyList<Subscription>> ListSubscriptionsAsync(CancellationToken cancellationToken)
    {
        var page = await _client.Value.Subscriptions.GetAsync(cancellationToken: cancellationToken);
        return page?.Value ?? [];
    }

    public async Task<ReceivedNotification> ToReceivedAsync(GraphNotification notification, CancellationToken cancellationToken)
    {
        var eventId = notification.ResourceData?.Id ?? "(unknown)";
        var sourceMailbox = GraphResourceParser.MailboxFromResource(notification.Resource)
            ?? _options.Mailbox;
        string? subject = null;
        string? organizer = null;
        string? start = null;
        string? end = null;
        string? detail = null;
        string? attendeeResponses = null;

        if (!string.IsNullOrWhiteSpace(notification.ResourceData?.Id)
            && !string.Equals(notification.ChangeType, "deleted", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var calendarEvent = await GetEventAsync(sourceMailbox, notification.ResourceData.Id, cancellationToken);
                subject = calendarEvent?.Subject;
                organizer = calendarEvent?.Organizer?.EmailAddress?.Address;
                start = calendarEvent?.Start?.DateTime;
                end = calendarEvent?.End?.DateTime;
                attendeeResponses = FormatAttendeeResponses(calendarEvent);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load event {EventId} from {Mailbox}.", eventId, sourceMailbox);
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
            Detail = detail,
            SourceMailbox = sourceMailbox,
            SubscriptionId = notification.SubscriptionId,
            Resource = notification.Resource,
            AttendeeResponses = attendeeResponses
        };
    }

    private static string? FormatAttendeeResponses(Event? calendarEvent)
    {
        if (calendarEvent?.Attendees is null || calendarEvent.Attendees.Count == 0)
        {
            return null;
        }

        return string.Join("; ", calendarEvent.Attendees
            .Where(attendee => !string.IsNullOrWhiteSpace(attendee.EmailAddress?.Address))
            .Select(attendee =>
            {
                var kind = attendee.Type == AttendeeType.Resource ? "room" : "attendee";
                var response = attendee.Status?.Response?.ToString() ?? "unknown";
                return $"{attendee.EmailAddress!.Address} ({kind}): {response}";
            }));
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
