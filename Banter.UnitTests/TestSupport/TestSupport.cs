using System.Runtime.CompilerServices;
using Banter.Application.Abstractions;
using Banter.Application.Abstractions.Ai;
using Banter.Application.Abstractions.Auth;
using Banter.Application.Abstractions.Bots;
using Banter.Application.Abstractions.Realtime;
using Banter.Domain.Users;
using Banter.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Banter.UnitTests.TestSupport;

internal sealed class TestDatabase : IAsyncDisposable
{
    public AppDbContext Context { get; }

    private TestDatabase(AppDbContext context)
    {
        Context = context;
    }

    public static TestDatabase Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"BanterTests-{Guid.NewGuid()}")
            .Options;

        return new TestDatabase(new AppDbContext(options));
    }

    public User AddUser(string email, string displayName, bool isBot = false)
    {
        var user = new User(email, displayName, null, isBot);

        Context.Users.Add(user);

        return user;
    }

    public User AddBot(string email, string displayName) => AddUser(email, displayName, isBot: true);

    public Task SaveAsync() => Context.SaveChangesAsync();

    public ValueTask DisposeAsync() => Context.DisposeAsync();
}

internal sealed class FakeUserContext(Guid userId) : IUserContext
{
    public Guid UserId { get; } = userId;
}

internal sealed class FakePresenceService : IPresenceService
{
    private readonly Dictionary<Guid, HashSet<string>> _connections = [];

    public FakePresenceService(params Guid[] onlineUserIds)
    {
        foreach (var userId in onlineUserIds)
            _connections[userId] = ["seed"];
    }

    public Task ConnectAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default)
    {
        if (!_connections.TryGetValue(userId, out var connections))
        {
            connections = [];
            _connections[userId] = connections;
        }

        connections.Add(connectionId);

        return Task.CompletedTask;
    }

    public Task DisconnectAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default)
    {
        if (_connections.TryGetValue(userId, out var connections))
        {
            connections.Remove(connectionId);

            if (connections.Count == 0)
                _connections.Remove(userId);
        }

        return Task.CompletedTask;
    }

    public Task HeartbeatAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default)
        => ConnectAsync(userId, connectionId, cancellationToken);

    public Task<bool> IsOnlineAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(_connections.ContainsKey(userId));

    public Task<int> GetConnectionCountAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(_connections.TryGetValue(userId, out var connections) ? connections.Count : 0);
}

internal sealed class FakeSignalRNotifier : ISignalRNotifier
{
    public List<CapturedMessage> Messages { get; } = [];

    public List<CapturedTyping> TypingEvents { get; } = [];

    public Task SendMessageAsync(
        MessageNotification message,
        IReadOnlyList<string> userIds,
        CancellationToken cancellationToken = default)
    {
        Messages.Add(new CapturedMessage(message, userIds));

        return Task.CompletedTask;
    }

    public Task SendBotTypingAsync(
        Guid conversationId,
        string botName,
        IReadOnlyList<string> userIds,
        CancellationToken cancellationToken = default)
    {
        TypingEvents.Add(new CapturedTyping(conversationId, botName, userIds));

        return Task.CompletedTask;
    }

    internal sealed record CapturedMessage(MessageNotification Message, IReadOnlyList<string> UserIds);

    internal sealed record CapturedTyping(Guid ConversationId, string BotName, IReadOnlyList<string> UserIds);
}

internal sealed class CapturingBotReplyQueue : IBotReplyQueue
{
    public List<BotReplyRequest> Requests { get; } = [];

    public bool TryEnqueue(BotReplyRequest request)
    {
        Requests.Add(request);

        return true;
    }

    public async IAsyncEnumerable<BotReplyRequest> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        yield break;
    }
}

internal sealed class RejectingBotReplyQueue : IBotReplyQueue
{
    public int Attempts { get; private set; }

    public bool TryEnqueue(BotReplyRequest request)
    {
        Attempts++;

        return false;
    }

    public async IAsyncEnumerable<BotReplyRequest> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        yield break;
    }
}

internal sealed class EmptyBotReplyQueue : IBotReplyQueue
{
    public bool TryEnqueue(BotReplyRequest request) => true;

    public async IAsyncEnumerable<BotReplyRequest> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        yield break;
    }
}

internal sealed class FakeAiChatClient(string reply) : IAiChatClient
{
    public BotChatRequest? Request { get; private set; }

    public Task<string> GenerateReplyAsync(BotChatRequest request, CancellationToken cancellationToken = default)
    {
        Request = request;

        return Task.FromResult(reply);
    }
}

internal sealed class ListLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Entries.Add((logLevel, formatter(state, exception), exception));
    }
}
