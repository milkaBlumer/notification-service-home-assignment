using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NotificationApi;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace NotificationApi.Tests;

public class NotificationApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public NotificationApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
        Storage.Seed();
    }

    [Fact]
    public async Task PostNotifications_WithMissingMessage_ReturnsBadRequest()
    {
        var request = new
        {
            targetChannels = new[] { new { type = "sms", value = "+15551234567" } }
        };

        var response = await _client.PostAsJsonAsync("/notifications", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task PostNotifications_WithInvalidChannel_ReturnsBadRequest()
    {
        var request = new
        {
            targetChannels = new[] { new { type = "", value = "" } },
            message = "test"
        };

        var response = await _client.PostAsJsonAsync("/notifications", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("errors", out var errors));
        Assert.Contains("type is required", errors[0].GetString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostNotifications_ReturnsCreatedAndLocation()
    {
        var request = new
        {
            targetChannels = new[] { new { type = "sms", value = "+15551234567" } },
            message = "Hello world"
        };

        var response = await _client.PostAsJsonAsync("/notifications", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains("/notifications/", response.Headers.Location!.ToString(), StringComparison.Ordinal);

        var notification = await response.Content.ReadFromJsonAsync<Notification>();
        Assert.NotNull(notification);
        Assert.Equal("Hello world", notification!.Message);
        Assert.Equal(1, notification.SmsSegments);
    }

    [Fact]
    public async Task PutNotifications_NonExistentId_ReturnsNotFound()
    {
        var request = new { message = "updated" };

        var response = await _client.PutAsJsonAsync("/notifications/9999", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostSend_NonExistentNotification_ReturnsNotFound()
    {
        var response = await _client.PostAsync("/notifications/9999/send", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PutNotifications_UpdatesSmsSegments_WhenMessageChanges()
    {
        var request = new { message = "A longer SMS message that spans segments" };

        var response = await _client.PutAsJsonAsync("/notifications/2", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var notification = await response.Content.ReadFromJsonAsync<Notification>();
        Assert.NotNull(notification);
        Assert.Equal(request.message, notification!.Message);
        Assert.Equal(SmsSegmenter.MinSegments(request.message), notification.SmsSegments);
    }
}
