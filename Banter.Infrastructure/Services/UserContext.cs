using Banter.Application.Abstractions;
using Banter.Application.Abstractions.Auth;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Banter.Infrastructure.Services;

internal class UserContext : IUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }
    public Guid UserId
    {
        get
        {
            string? userIdValue = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(userIdValue, out Guid userId))
                throw new AppException("Users.NotAuthinticated", "The curent user is not authenticated");

            return userId;
        }
    }
}
