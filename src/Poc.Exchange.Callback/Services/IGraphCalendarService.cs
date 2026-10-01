using Microsoft.Graph.Models;
using Poc.Exchange.Callback.Models;

namespace Poc.Exchange.Callback.Services;

public interface IGraphCalendarService
{
    Task<Event?> GetEventAsync(string mailbox, string eventId, CancellationToken cancellationToken);

    Task<Subscription> CreateSubscriptionAsync(string mailbox, CancellationToken cancellationToken);

    Task<IReadOnlyList<Subscription>> ListSubscriptionsAsync(CancellationToken cancellationToken);

    Task<ReceivedNotification> ToReceivedAsync(GraphNotification notification, CancellationToken cancellationToken);
}
