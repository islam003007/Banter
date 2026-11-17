using Banter.Application.Abstractions;
using Banter.Application.Abstractions.Auth;
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

    public User AddUser(string email, string displayName)
    {
        var user = new User(email, displayName, null);

        Context.Users.Add(user);

        return user;
    }

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
