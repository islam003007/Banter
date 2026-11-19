using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace Banter.IntegrationTests.Endpoints;

public class BotTests : IntegrationTestBase
{
    public BotTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task HumanToBotDirectMessage_EmitsTypingAndPersistsOneReply()
    {
        var human = await Factory.CreateUserAsync();

        var botId = await QueryDbAsync(database =>
            database.Users.Where(user => user.Email == "ada@bots.local").Select(user => user.Id).SingleAsync());

        var conversationId = await CreateDirectConversationAsync(human.Client, botId);

        await using var connection = await ConnectAsync(human);

        var typing = new TaskCompletionSource<(Guid ConversationId, string BotName)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On<Guid, string>("ReceiveBotTyping", (id, botName) =>
            typing.TrySetResult((id, botName)));
        connection.On<JsonElement>("ReceiveMessage", payload => received.TrySetResult(payload));

        var sent = await human.Client.PostAsJsonAsync(
            $"/api/v1/conversations/{conversationId}/messages",
            new { Content = "Hi Ada, how should I queue this?" });

        sent.EnsureSuccessStatusCode();

        var typingPayload = await typing.Task.WaitAsync(TimeSpan.FromSeconds(10));

        typingPayload.ConversationId.Should().Be(conversationId);
        typingPayload.BotName.Should().Be("Ada");

        var reply = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));

        reply.GetProperty("conversationId").GetGuid().Should().Be(conversationId);
        reply.GetProperty("userId").GetGuid().Should().Be(botId);
        reply.GetProperty("isBot").GetBoolean().Should().BeTrue();
        reply.GetProperty("content").GetString().Should().NotBeNullOrWhiteSpace();

        await WaitUntilAsync(
            async () => await QueryDbAsync(database => database.Messages.CountAsync(
                message => message.ConversationId == conversationId && message.UserId == botId)) == 1,
            because: "exactly one bot reply should be persisted");

        var conversation = await QueryDbAsync(database => database.Conversations
            .SingleAsync(c => c.Id == conversationId));

        conversation.LastMessageId.Should().NotBeNull();
    }

    [Fact]
    public async Task BotInAGroupConversation_NeverReplies()
    {
        var ahmed = await Factory.CreateUserAsync();
        var sara = await Factory.CreateUserAsync();

        var botId = await QueryDbAsync(database =>
            database.Users.Where(user => user.Email == "ada@bots.local").Select(user => user.Id).SingleAsync());

        var conversationId = await CreateGroupConversationAsync(ahmed.Client, [sara.UserId, botId], "Bots stay out");

        var sent = await ahmed.Client.PostAsJsonAsync(
            $"/api/v1/conversations/{conversationId}/messages",
            new { Content = "Ada, can you answer in this group?" });

        sent.EnsureSuccessStatusCode();

        // A bot reply is deliberately delayed 1200-1800 ms when queued at all. Waiting past that
        // window proves the group message never reached the bot queue.
        await Task.Delay(TimeSpan.FromMilliseconds(2500));

        var botMessages = await QueryDbAsync(database => database.Messages.CountAsync(
            message => message.ConversationId == conversationId && message.UserId == botId));

        botMessages.Should().Be(0);
    }

    [Fact]
    public async Task BotLogin_IsBlockedByTheApi()
    {
        // Bots have no password hash; even a caller who knows the seed email must not get a
        // session. This is the integration counterpart to the handler-level guard.
        using var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = "milo@bots.local",
            Password = "Password@123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await ReadProblemAsync(response);
        problem.Title.Should().Be("Auth.loginFailed");
    }

    private async Task<HubConnection> ConnectAsync(TestUser user)
    {
        using var loginClient = Factory.CreateClient();
        var login = await LoginAsync(loginClient, user.Email, user.Password);
        var cookie = CookieHeader(login);

        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(Factory.Server.BaseAddress, "/hubs/chat"), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
                options.Headers["Cookie"] = cookie;
            })
            .Build();

        await connection.StartAsync().WaitAsync(TimeSpan.FromSeconds(15));

        return connection;
    }

    private static async Task<Guid> CreateGroupConversationAsync(
        HttpClient client,
        IReadOnlyList<Guid> participantIds,
        string title)
    {
        var response = await client.PostAsJsonAsync("/api/v1/conversations", new
        {
            ParticipantsIds = participantIds,
            GroupTitle = title
        });

        return await ReadGuidAsync(response);
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
}
