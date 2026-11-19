namespace Banter.Application.Constants;

/// <summary>
/// Presence timing shared by the Redis implementation, the hub heartbeat and the tests.
/// The client beats every 25 seconds; a two-minute window tolerates a few missed beats and
/// lets Redis expire a crashed server's state on its own.
/// </summary>
public static class PresenceConstants
{
    public static readonly TimeSpan ConnectionTimeout = TimeSpan.FromMinutes(2);

    public const string KeyPrefix = "presence:";
}
