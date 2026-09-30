using Poc.Exchange.Callback.Models;

namespace Poc.Exchange.Callback.Services;

public interface INotificationStore
{
    void Add(ReceivedNotification notification);

    IReadOnlyList<ReceivedNotification> ListRecent(int take = 50);
}
