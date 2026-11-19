namespace Banter.Application.Abstractions;

/// <summary>
/// Tracks which users currently have at least one open SignalR connection.
///
/// Presence is connection-based: a user may have several tabs/devices connected at once, so a
/// disconnect only marks the user offline when their last connection goes away. Implementations
/// keep a heartbeat/TTL per connection so a crashed server cannot leave a user online forever.
/// </summary>
public interface IPresenceService
{
    /// <summary>Registers one new connection for the user (or refreshes it).</summary>
    Task ConnectAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default);

    /// <summary>Removes one connection. Other connections for the same user keep them online.</summary>
    Task DisconnectAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default);

    /// <summary>Refreshes the TTL of one connection. Clients call this periodically.</summary>
    Task HeartbeatAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default);

    /// <summary>True when the user has at least one non-stale connection.</summary>
    Task<bool> IsOnlineAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Number of non-stale connections for a user (0 when offline).</summary>
    Task<int> GetConnectionCountAsync(Guid userId, CancellationToken cancellationToken);
}
