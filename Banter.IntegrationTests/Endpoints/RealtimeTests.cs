using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace Banter.IntegrationTests.Endpoints;

public class RealtimeTests : IntegrationTestBase
{
    public RealtimeTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Presence_TracksMultipleConnectionsAndOnlyGoesOfflineAfterTheLastDisconnect()
    {
        var observer = await Factory.CreateUserAsync();
        var target = await Factory.CreateUserAsync();

        var conversationId = await CreateDirectConversationAsync(observer.Client, target.UserId);

        (await IsOnlineAsync(observer.Client, conversationId, target.UserId)).Should().BeFalse();
        (await Factory.GetConnectionCountAsync(target.UserId)).Should().Be(0);

        await using var first = await ConnectAsync(target);

        await WaitUntilAsync(
            async () => await Factory.GetConnectionCountAsync(target.UserId) == 1,
            because: "the first hub connection should register presence");

        (await IsOnlineAsync(observer.Client, conversationId, target.UserId)).Should().BeTrue();

        await using var second = await ConnectAsync(target);

        await WaitUntilAsync(
            async () => await Factory.GetConnectionCountAsync(target.UserId) == 2,
            because: "a second tab should add a second presence connection");

        await first.StopAsync();

        await WaitUntilAsync(
            async () => await Factory.GetConnectionCountAsync(target.UserId) == 1,
            because: "closing one tab must leave the other connection online");

        (await IsOnlineAsync(observer.Client, conversationId, target.UserId)).Should().BeTrue();

        await second.StopAsync();

        await WaitUntilAsync(
            async () => await Factory.GetConnectionCountAsync(target.UserId) == 0,
            because: "closing the last tab should remove presence");

        (await IsOnlineAsync(observer.Client, conversationId, target.UserId)).Should().BeFalse();
    }

    [Fact]
    public async Task Presence_PrunesStaleConnectionsLeftByACrashedServer()
    {
        var user = await Factory.CreateUserAsync();

        var redis = Factory.Services.GetRequiredService<StackExchange.Redis.IConnectionMultiplexer>().GetDatabase();
        var key = $"{Banter.Application.Constants.PresenceConstants.KeyPrefix}{user.UserId:N}";
        var staleTimestamp = DateTimeOffset.UtcNow
            .Add(-Banter.Application.Constants.PresenceConstants.ConnectionTimeout)
            .AddMinutes(-1)
            .ToUnixTimeMilliseconds();

        await redis.HashSetAsync(key, "connection-from-a-dead-server", staleTimestamp);
        await redis.KeyExpireAsync(key, Banter.Application.Constants.PresenceConstants.ConnectionTimeout);

        (await Factory.GetConnectionCountAsync(user.UserId)).Should().Be(0);

        (await redis.KeyExistsAsync(key)).Should().BeFalse(
            "reading presence should remove a hash left with only stale connections");
    }

    [Fact]
    public async Task ReceiveMessage_DeliversThePersistedMessageToTheOtherParticipant()
    {
        var recipient = await Factory.CreateUserAsync();
        var sender = await Factory.CreateUserAsync();

        var conversationId = await CreateDirectConversationAsync(recipient.Client, sender.UserId);

        await using var connection = await ConnectAsync(recipient);

        var received = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On<JsonElement>("ReceiveMessage", payload => received.TrySetResult(payload));

        var response = await sender.Client.PostAsJsonAsync(
            $"/api/v1/conversations/{conversationId}/messages",
            new { Content = "Realtime hello" });

        var messageId = await response.Content.ReadFromJsonAsync<Guid>();
        var payload = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));

        payload.GetProperty("id").GetGuid().Should().Be(messageId);
        payload.GetProperty("conversationId").GetGuid().Should().Be(conversationId);
        payload.GetProperty("userId").GetGuid().Should().Be(sender.UserId);
        payload.GetProperty("content").GetString().Should().Be("Realtime hello");
        payload.GetProperty("isBot").GetBoolean().Should().BeFalse();
        payload.GetProperty("senderDisplayName").GetString().Should().Be(sender.DisplayName);
    }

    [Fact]
    public async Task UnauthenticatedHubConnection_IsRejected()
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(Factory.Server.BaseAddress, "/hubs/chat"), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
            })
            .Build();

        await using (connection)
        {
            await Assert.ThrowsAnyAsync<Exception>(() =>
                connection.StartAsync().WaitAsync(TimeSpan.FromSeconds(10)));
        }
    }

    private async Task<HubConnection> ConnectAsync(TestUser user)
    {
        using var loginClient = Factory.CreateClient();
        var login = await LoginAsync(loginClient, user.Email, user.Password);
        var cookie = CookieHeader(login);

        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(Factory.Server.BaseAddress, "/hubs/chat"), options =>
            {
                // TestServer has no real WebSocket listener; long polling exercises the same hub,
                // auth, presence and backplane paths over the in-memory transport.
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
                options.Headers["Cookie"] = cookie;
            })
            .Build();

        await connection.StartAsync().WaitAsync(TimeSpan.FromSeconds(15));

        return connection;
    }

    private static async Task<bool> IsOnlineAsync(HttpClient client, Guid conversationId, Guid userId)
    {
        var detail = await client.GetFromJsonAsync<ConversationDetail>($"/api/v1/conversations/{conversationId}");

        return detail!.Participants.Single(participant => participant.UserId == userId).IsOnline;
    }

    private static async Task<Guid> CreateDirectConversationAsync(HttpClient client, Guid participantId)
    {
        var response = await client.PostAsJsonAsync("/api/v1/conversations", new
        {
            ParticipantsIds = new[] { participantId },
            GroupTitle = (string?)null
        });

        return await ReadGuidAsync(response);
    }

    private sealed record ConversationDetail(
        Guid ConversationId,
        string? Title,
        bool IsGroup,
        IReadOnlyList<ConversationParticipant> Participants);

    private sealed record ConversationParticipant(
        Guid UserId,
        string DisplayName,
        string? ProfilePictureUrl,
        bool IsOnline,
        bool IsBot);
}
