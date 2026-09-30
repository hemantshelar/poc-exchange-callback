using System.Collections.Concurrent;
using Poc.Exchange.Callback.Models;

namespace Poc.Exchange.Callback.Services;

public sealed class InMemoryNotificationStore : INotificationStore
{
    private readonly ConcurrentQueue<ReceivedNotification> _items = new();
    private const int MaxItems = 100;

    public void Add(ReceivedNotification notification)
    {
        _items.Enqueue(notification);
        while (_items.Count > MaxItems && _items.TryDequeue(out _))
        {
        }
    }

    public IReadOnlyList<ReceivedNotification> ListRecent(int take = 50)
    {
        return _items.Reverse().Take(take).ToArray();
    }
}
