using Banter.Application.Abstractions;
using Banter.Application.Constants;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Banter.Infrastructure.Services;

/// <summary>
/// Redis-backed, connection-aware presence.
///
/// A user is a hash at <c>presence:{userId}</c> whose fields are connection ids and whose values
/// are the last heartbeat (Unix milliseconds). The hash key carries a TTL refreshed by every
/// heartbeat, while each field is pruned independently once it is older than
/// <see cref="PresenceConstants.ConnectionTimeout"/>. That combination supports multiple tabs,
/// removes a user only after their last connection disconnects, and cleans up state left behind by
/// a crashed instance. Presence is best-effort: Redis failures are logged and treated as offline so
/// inbox/search reads keep working during a cache outage.
/// </summary>
internal sealed class PresenceService(
    IConnectionMultiplexer _redis,
    ILogger<PresenceService> _logger) : IPresenceService
{
    private static RedisKey Key(Guid userId) => $"{PresenceConstants.KeyPrefix}{userId:N}";

    public Task ConnectAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default)
        => TouchAsync(userId, connectionId, cancellationToken);

    public Task HeartbeatAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default)
        => TouchAsync(userId, connectionId, cancellationToken);

    public async Task DisconnectAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var database = _redis.GetDatabase();
            var key = Key(userId);

            await database.HashDeleteAsync(key, connectionId);
            await RemoveKeyWhenEmptyAsync(database, key, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Could not remove presence connection {ConnectionId} for user {UserId}", connectionId, userId);
        }
    }

    public async Task<bool> IsOnlineAsync(Guid userId, CancellationToken cancellationToken)
        => await GetConnectionCountAsync(userId, cancellationToken) > 0;

    public async Task<int> GetConnectionCountAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var database = _redis.GetDatabase();
            var key = Key(userId);

            var entries = await database.HashGetAllAsync(key);

            if (entries.Length == 0)
                return 0;

            var cutoff = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                - (long)PresenceConstants.ConnectionTimeout.TotalMilliseconds;

            var active = 0;
            var staleFields = new List<RedisValue>();

            foreach (var entry in entries)
            {
                if (long.TryParse(entry.Value.ToString(), out var lastSeen) && lastSeen >= cutoff)
                    active++;
                else
                    staleFields.Add(entry.Name);
            }

            if (staleFields.Count > 0)
            {
                await database.HashDeleteAsync(key, staleFields.ToArray());

                if (active == 0)
                    await database.KeyDeleteAsync(key);
            }

            return active;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Could not read presence for user {UserId}", userId);

            return 0;
        }
    }

    private async Task TouchAsync(Guid userId, string connectionId, CancellationToken cancellationToken)
    {
        try
        {
            var database = _redis.GetDatabase();
            var key = Key(userId);
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Hash first, then expire: expiring a missing key would be a no-op and the hash would
            // then live without a TTL. Both calls are cheap and heartbeat-driven.
            await database.HashSetAsync(key, connectionId, now);
            await database.KeyExpireAsync(key, PresenceConstants.ConnectionTimeout);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Could not refresh presence connection {ConnectionId} for user {UserId}", connectionId, userId);
        }
    }

    private static async Task RemoveKeyWhenEmptyAsync(IDatabase database, RedisKey key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (await database.HashLengthAsync(key) == 0)
            await database.KeyDeleteAsync(key);
    }
}
