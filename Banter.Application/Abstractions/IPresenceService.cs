namespace Banter.Application.Abstractions;

public interface IPresenceService
{
    Task SetOnlineAsync(string userId);
    Task SetOfflineAsync(string userId);
    Task<bool> IsOnlineAsync(string userId, CancellationToken cancellationToken);
}
