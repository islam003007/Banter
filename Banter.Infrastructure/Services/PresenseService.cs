using Banter.Application.Abstractions;
using Microsoft.Extensions.Caching.Distributed;

namespace Banter.Infrastructure.Services;

internal class PresenseService(IDistributedCache _cache) : IPresenceService
{
    private static DistributedCacheEntryOptions _defaultOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
    };

    private static string Key(Guid userId) => $"Presence:{userId}";
    public async Task<bool> IsOnlineAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _cache.GetStringAsync(Key(userId), cancellationToken) != null;
    }

    public Task SetOfflineAsync(Guid userId)
    {
        return _cache.RemoveAsync(Key(userId));
    }

    public Task SetOnlineAsync(Guid userId)
    {
        return _cache.SetStringAsync(Key(userId), "1", _defaultOptions);
    }
}
