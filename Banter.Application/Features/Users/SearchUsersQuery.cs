using Banter.Application.Abstractions;
using Banter.Application.Abstractions.Data;
using Banter.Application.Abstractions.Messaging;
using Banter.Application.Constants;
using Banter.Application.Extensions;
using Banter.SharedKernel;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Banter.Application.Features.Users;

internal record SearchUsersQuery(string SearchTerm, int PageSize, int PageNumber) : IQuery<IReadOnlyList<SearchUsersResponse>>;

internal class SearchUsersQueryValidator : AbstractValidator<SearchUsersQuery>
{
    public SearchUsersQueryValidator()
    {
        RuleFor(x => x.SearchTerm)
            .NotEmpty();

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, PaginationConstants.MaxPageSize);

        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1);
    }
}

public record SearchUsersResponse(Guid Id, string DisplayName, string Email, string? ProfilePicture, bool IsOnline);

internal class SearchUsersHandler(IAppDbContext _dbContext, IPresenceService presenceService)
    : IQueryHandler<SearchUsersQuery, IReadOnlyList<SearchUsersResponse>>
{
    public async Task<Result<IReadOnlyList<SearchUsersResponse>>> Handle(SearchUsersQuery request, CancellationToken cancellationToken)
    {

        var users = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.DisplayName.Contains(request.SearchTerm) || u.Email!.Contains(request.SearchTerm))
            .Select(x => new
            {
                x.Id,
                x.DisplayName,
                x.ProfilePictureUrl,
                x.Email
            })
            .Skip(request.PageSize * (request.PageNumber - 1))
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var usersIsOnline = await Task.WhenAll(users.Select(x => presenceService.IsOnlineAsync(x.Id, cancellationToken)));

        var results = users
            .Zip(usersIsOnline, (user, IsOnline) => new SearchUsersResponse(user.Id, user.DisplayName, user.Email!, user.ProfilePictureUrl, IsOnline))
            .ToList();

        return results;
    }
}


