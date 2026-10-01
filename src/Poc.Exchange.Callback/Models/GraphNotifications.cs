using System.Text.Json.Serialization;

namespace Poc.Exchange.Callback.Models;

public sealed class GraphNotificationPayload
{
    [JsonPropertyName("value")]
    public List<GraphNotification> Value { get; set; } = [];
}

public sealed class GraphNotification
{
    [JsonPropertyName("subscriptionId")]
    public string? SubscriptionId { get; set; }

    [JsonPropertyName("clientState")]
    public string? ClientState { get; set; }

    [JsonPropertyName("changeType")]
    public string? ChangeType { get; set; }

    [JsonPropertyName("resource")]
    public string? Resource { get; set; }

    [JsonPropertyName("resourceData")]
    public GraphResourceData? ResourceData { get; set; }
}

public sealed class GraphResourceData
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("@odata.id")]
    public string? ODataId { get; set; }
}

public sealed class ReceivedNotification
{
    public required DateTimeOffset ReceivedAtUtc { get; init; }
    public required string ChangeType { get; init; }
    public required string EventId { get; init; }
    public string? Subject { get; init; }
    public string? Organizer { get; init; }
    public string? Start { get; init; }
    public string? End { get; init; }
    public string? Detail { get; init; }
    public string? SourceMailbox { get; init; }
    public string? SubscriptionId { get; init; }
    public string? Resource { get; init; }
    public string? AttendeeResponses { get; init; }
}
