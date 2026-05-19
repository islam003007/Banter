using Banter.Application.Abstractions;
using Banter.Application.Abstractions.Data;
using Banter.Application.Abstractions.Messaging;
using Banter.Application.Constants;
using Banter.Application.Errors;
using Banter.Application.Extensions;
using Banter.Application.Features.Common;
using Banter.SharedKernel;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Banter.Application.Features.Users;

internal record SearchUsersQuery(string SearchTerm, string? Cursor, int PageSize) : IQuery<SearchUsersResponse>;

internal class SearchUsersQueryValidator : AbstractValidator<SearchUsersQuery>
{
    public SearchUsersQueryValidator()
    {
        RuleFor(x => x.SearchTerm)
            .NotEmpty()
            .MinimumLength(2);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, PaginationConstants.MaxPageSize);

        RuleFor(x => x.Cursor)
            .Must(x => x is null || !string.IsNullOrWhiteSpace(x)).WithMessage("Cursor must either be null or non-empty");
    }
}

public record SearchUsersResponse(IReadOnlyList<SearchUsersResponse.UserResponse> Users, string? NextCursor, bool HasMore)
{
    public record UserResponse(Guid Id, string DisplayName, string Email, string? ProfilePicture, bool IsOnline);
}


internal class SearchUsersHandler(IAppDbContext _dbContext, IPresenceService presenceService)
    : IQueryHandler<SearchUsersQuery, SearchUsersResponse>
{
    public async Task<Result<SearchUsersResponse>> Handle(SearchUsersQuery request, CancellationToken cancellationToken)
    {

        PageCursor? decodedCursor = null;

        if (request.Cursor is not null)
        {
            decodedCursor = PageCursor.Decode(request.Cursor);

            if (decodedCursor is null)
                return Result.Failure<SearchUsersResponse>(PageErrors.InvalidCursor);
        }

        // For postgres perhaps there are better solutions to implement searching.
        var users = await _dbContext.Users
            .Where(u => u.DisplayName.Contains(request.SearchTerm) || u.Email!.Contains(request.SearchTerm))
            .Where(u => decodedCursor == null
                    || u.CreatedAt < decodedCursor.CreatedAt
                    || (u.CreatedAt == decodedCursor.CreatedAt && u.Id < decodedCursor.Id))
            .Select(u => new
            {
                u.Id,
                u.DisplayName,
                u.ProfilePictureUrl,
                u.Email,
                u.CreatedAt
            })
            .OrderByDescending(u => u.CreatedAt).ThenByDescending(u => u.Id)
            .Take(request.PageSize + 1)
            .ToListAsync(cancellationToken);

        var hasMore = PaginationHelpers.Slice(users, request.PageSize);

        var nextCursor = PaginationHelpers.CreateNextCursor(
            users,
            hasMore,
            x => x.CreatedAt,
            x => x.Id);

        var usersIsOnline = await Task.WhenAll(users.Select(x => presenceService.IsOnlineAsync(x.Id, cancellationToken)));

        var results = users
            .Zip(usersIsOnline, (user, IsOnline) 
            => new SearchUsersResponse.UserResponse(user.Id, user.DisplayName, user.Email!, user.ProfilePictureUrl, IsOnline))
            .ToList();

        return new SearchUsersResponse(results, nextCursor, hasMore);
    }
}


