namespace Poc.Exchange.Callback.Models;

public sealed class SubscriptionRequest
{
    public string? Mailbox { get; set; }
}

public sealed class SubscriptionResult
{
    public string? Id { get; set; }
    public string? Resource { get; set; }
    public string? NotificationUrl { get; set; }
    public DateTimeOffset? ExpirationDateTime { get; set; }
    public string? ChangeType { get; set; }
    public string? ClientState { get; set; }
    public string? Mailbox { get; set; }
}
