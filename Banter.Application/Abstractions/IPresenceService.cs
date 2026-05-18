namespace Banter.Application.Abstractions;

public interface IPresenceService
{
    Task SetOnlineAsync(Guid userId);
    Task SetOfflineAsync(Guid userId);
    Task<bool> IsOnlineAsync(Guid userId, CancellationToken cancellationToken);
}
