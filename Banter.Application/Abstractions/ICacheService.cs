namespace Banter.Application.Abstractions;

public interface ICacheService
{
    public ValueTask<T> GetOrCreateAsync<T>(string key,
        Func<CancellationToken, ValueTask<T>> factory,
        CancellationToken cancellationToken,
        IEnumerable<string>? tags = null,
        TimeSpan? localExpiration = null,
        TimeSpan? expiration = null);
}
