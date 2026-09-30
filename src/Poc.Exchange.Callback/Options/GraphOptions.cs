using System.ComponentModel.DataAnnotations;

namespace Poc.Exchange.Callback.Options;

public sealed class GraphOptions
{
    public const string SectionName = "Graph";

    [Required]
    public string TenantId { get; set; } = string.Empty;

    [Required]
    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    [Required]
    public string Mailbox { get; set; } = "admin@harmoniousflair.com.au";

    [Required]
    public string ClientState { get; set; } = string.Empty;

    /// <summary>
    /// Public HTTPS URL Graph will POST to, including path
    /// (e.g. https://app.azurewebsites.net/api/graph/notifications).
    /// </summary>
    public string NotificationUrl { get; set; } = string.Empty;
}
