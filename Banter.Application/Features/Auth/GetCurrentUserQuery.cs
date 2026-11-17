using Banter.Application.Abstractions.Auth;
using Banter.Application.Abstractions.Data;
using Banter.Application.Abstractions.Messaging;
using Banter.Application.Errors;
using Banter.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Banter.Application.Features.Auth;

public record GetCurrentUserQuery : IQuery<CurrentUserResponse>;

public record CurrentUserResponse(Guid Id, string Email, string DisplayName, string? ProfilePictureUrl);

internal class GetCurrentUserQueryHandler(IAppDbContext _dbContext, IUserContext _userContext)
    : IQueryHandler<GetCurrentUserQuery, CurrentUserResponse>
{
    public async Task<Result<CurrentUserResponse>> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var userId = _userContext.UserId;

        var user = await _dbContext.Users
            .Where(u => u.Id == userId)
            .Select(u => new CurrentUserResponse(u.Id, u.Email!, u.DisplayName, u.ProfilePictureUrl))
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
            return Result.Failure<CurrentUserResponse>(AuthErrors.UserNotFound);

        return user;
    }
}
