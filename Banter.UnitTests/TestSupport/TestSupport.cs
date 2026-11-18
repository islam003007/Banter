using System.Runtime.CompilerServices;
using Banter.Application.Abstractions;
using Banter.Application.Abstractions.Ai;
using Banter.Application.Abstractions.Auth;
using Banter.Application.Abstractions.Bots;
using Banter.Application.Abstractions.Realtime;
using Banter.Domain.Users;
using Banter.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

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

internal sealed class FakePresenceService(params Guid[] onlineUserIds) : IPresenceService
{
    private readonly HashSet<Guid> _onlineUsers = [.. onlineUserIds];

    public Task SetOnlineAsync(Guid userId)
    {
        _onlineUsers.Add(userId);

        return Task.CompletedTask;
    }

    public Task SetOfflineAsync(Guid userId)
    {
        _onlineUsers.Remove(userId);

        return Task.CompletedTask;
    }

    public Task<bool> IsOnlineAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(_onlineUsers.Contains(userId));
}

internal sealed class FakeSignalRNotifier : ISignalRNotifier
{
    public List<CapturedMessage> Messages { get; } = [];

    public List<CapturedTyping> TypingEvents { get; } = [];

    public Task SendMessageAsync(MessageNotification message, IReadOnlyList<string> userIds)
    {
        Messages.Add(new CapturedMessage(message, userIds));

        return Task.CompletedTask;
    }

    public Task SendBotTypingAsync(Guid conversationId, string botName, IReadOnlyList<string> userIds)
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

internal sealed class FakeAiChatClient(string reply) : IAiChatClient
{
    public BotChatRequest? Request { get; private set; }

    public Task<string> GenerateReplyAsync(BotChatRequest request, CancellationToken cancellationToken = default)
    {
        Request = request;

        return Task.FromResult(reply);
    }
}
