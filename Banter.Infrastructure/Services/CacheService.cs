using Banter.Application.Abstractions;
using Microsoft.Extensions.Caching.Hybrid;

namespace Banter.Infrastructure.Services;

internal class CacheService(HybridCache _hybridCache) : ICacheService
{
    public ValueTask<T> GetOrCreateAsync<T>(string key,
        Func<CancellationToken, ValueTask<T>> factory,
        CancellationToken cancellationToken,
        IEnumerable<string>? tags = null,
        TimeSpan? localExpiration = null,
        TimeSpan? expiration = null)
    {
        HybridCacheEntryOptions? options = null;

        if (localExpiration.HasValue || expiration.HasValue)
        {
            options = new()
            {
                LocalCacheExpiration = localExpiration,
                Expiration = expiration
            };
        }
        return _hybridCache.GetOrCreateAsync(key, factory, options:options, tags:tags, cancellationToken: cancellationToken);
    }
}
