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
                  "resource": "Users/admin/Events/evt-1",
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
        Assert.Contains(recent, n => n.EventId == "evt-1" && n.ChangeType == "created");
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
    public Task<Subscription> CreateSubscriptionAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(new Subscription { Id = "sub-test" });
    }

    public Task<Event?> GetEventAsync(string eventId, CancellationToken cancellationToken)
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
            Subject = "fake"
        });
    }
}
