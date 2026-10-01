using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graph.Models;
using Poc.Exchange.Callback.Models;
using Poc.Exchange.Callback.Services;

namespace Poc.Exchange.Callback.Tests;

public sealed class NotificationEndpointTests : IClassFixture<CallbackApiFactory>
{
    private readonly HttpClient _client;

    public NotificationEndpointTests(CallbackApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_returns_ok()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Notifications_page_is_served()
    {
        var response = await _client.GetAsync("/notifications.html");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Notification history", html);
        Assert.Contains("id=\"search\"", html);
        Assert.Contains("id=\"changeType\"", html);
        Assert.Contains("id=\"sort\"", html);
    }

    [Fact]
    public async Task Graph_validation_echoes_token_as_plain_text()
    {
        var response = await _client.PostAsync(
            "/api/graph/notifications?validationToken=abc-123",
            new StringContent(string.Empty));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("abc-123", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Graph_notification_with_matching_client_state_is_stored()
    {
        var payload = """
            {
              "value": [
                {
                  "subscriptionId": "sub-1",
                  "clientState": "test-client-state",
                  "changeType": "created",
                  "resource": "Users/MeetingRoom1@harmoniousflair.com.au/Events/evt-1",
                  "resourceData": { "id": "evt-1" }
                }
              ]
            }
            """;

        var response = await _client.PostAsync(
            "/api/graph/notifications",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var recent = await _client.GetFromJsonAsync<List<ReceivedNotification>>("/api/notifications");
        Assert.NotNull(recent);
        Assert.Contains(recent, n =>
            n.EventId == "evt-1"
            && n.ChangeType == "created"
            && n.SourceMailbox == "MeetingRoom1@harmoniousflair.com.au"
            && n.AttendeeResponses != null
            && n.AttendeeResponses.Contains("accepted", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Subscriptions_page_is_served()
    {
        var response = await _client.GetAsync("/subscriptions.html");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Graph subscriptions", html);
        Assert.Contains("id=\"mailbox\"", html);
        Assert.Contains("Last create result", html);
    }

    [Fact]
    public async Task Create_subscription_uses_requested_mailbox_and_returns_graph_fields()
    {
        var response = await _client.PostAsJsonAsync("/api/subscriptions", new { mailbox = "room@harmoniousflair.com.au" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<SubscriptionResult>();
        Assert.NotNull(payload);
        Assert.Equal("sub-room@harmoniousflair.com.au", payload.Id);
        Assert.Equal("room@harmoniousflair.com.au", payload.Mailbox);
        Assert.Equal("/users/room@harmoniousflair.com.au/events", payload.Resource);
        Assert.NotNull(payload.ExpirationDateTime);
    }

    [Fact]
    public async Task List_subscriptions_returns_items()
    {
        var items = await _client.GetFromJsonAsync<List<SubscriptionResult>>("/api/subscriptions");
        Assert.NotNull(items);
        Assert.NotEmpty(items);
        Assert.Equal("sub-existing", items[0].Id);
    }

    [Fact]
    public async Task Graph_notification_with_wrong_client_state_is_ignored()
    {
        var payload = """
            {
              "value": [
                {
                  "clientState": "wrong",
                  "changeType": "created",
                  "resourceData": { "id": "should-ignore" }
                }
              ]
            }
            """;

        var response = await _client.PostAsync(
            "/api/graph/notifications",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var recent = await _client.GetFromJsonAsync<List<ReceivedNotification>>("/api/notifications");
        Assert.DoesNotContain(recent ?? [], n => n.EventId == "should-ignore");
    }
}

public sealed class CallbackApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var projectDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Poc.Exchange.Callback"));
        builder.UseContentRoot(projectDir);
        builder.UseWebRoot(Path.Combine(projectDir, "wwwroot"));
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Graph:TenantId"] = "00000000-0000-0000-0000-000000000001",
                ["Graph:ClientId"] = "00000000-0000-0000-0000-000000000002",
                ["Graph:ClientSecret"] = "test-secret",
                ["Graph:Mailbox"] = "admin@harmoniousflair.com.au",
                ["Graph:ClientState"] = "test-client-state",
                ["Graph:NotificationUrl"] = "https://localhost/api/graph/notifications"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IGraphCalendarService, FakeGraphCalendarService>();
        });
    }
}

file sealed class FakeGraphCalendarService : IGraphCalendarService
{
    public Task<Subscription> CreateSubscriptionAsync(string mailbox, CancellationToken cancellationToken)
    {
        return Task.FromResult(new Subscription
        {
            Id = $"sub-{mailbox}",
            Resource = $"/users/{mailbox}/events",
            NotificationUrl = "https://localhost/api/graph/notifications",
            ExpirationDateTime = DateTimeOffset.UtcNow.AddHours(70),
            ChangeType = "created,updated,deleted",
            ClientState = "test-client-state"
        });
    }

    public Task<IReadOnlyList<Subscription>> ListSubscriptionsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Subscription> items =
        [
            new Subscription
            {
                Id = "sub-existing",
                Resource = "/users/admin@harmoniousflair.com.au/events",
                NotificationUrl = "https://localhost/api/graph/notifications",
                ExpirationDateTime = DateTimeOffset.UtcNow.AddHours(10),
                ChangeType = "created,updated,deleted"
            }
        ];
        return Task.FromResult(items);
    }

    public Task<Event?> GetEventAsync(string mailbox, string eventId, CancellationToken cancellationToken)
    {
        return Task.FromResult<Event?>(null);
    }

    public Task<ReceivedNotification> ToReceivedAsync(GraphNotification notification, CancellationToken cancellationToken)
    {
        return Task.FromResult(new ReceivedNotification
        {
            ReceivedAtUtc = DateTimeOffset.UtcNow,
            ChangeType = notification.ChangeType ?? "unknown",
            EventId = notification.ResourceData?.Id ?? "(unknown)",
            Subject = "fake",
            SourceMailbox = GraphResourceParser.MailboxFromResource(notification.Resource),
            SubscriptionId = notification.SubscriptionId,
            Resource = notification.Resource,
            AttendeeResponses = "MeetingRoom1@harmoniousflair.com.au (room): accepted"
        });
    }
}

public sealed class GraphResourceParserTests
{
    [Theory]
    [InlineData("Users/MeetingRoom1@harmoniousflair.com.au/Events/abc", "MeetingRoom1@harmoniousflair.com.au")]
    [InlineData("/users/admin@harmoniousflair.com.au/events", "admin@harmoniousflair.com.au")]
    public void MailboxFromResource_reads_user(string resource, string expected)
    {
        Assert.Equal(expected, GraphResourceParser.MailboxFromResource(resource));
    }
}
