using Banter.Application.Abstractions;
using Banter.Application.Abstractions.Data;
using Banter.Application.Abstractions.Messaging;
using Banter.Application.Bots;
using Banter.Application.Constants;
using Banter.Application.Errors;
using Banter.Application.Extensions;
using Banter.Application.Features.Common;
using Banter.SharedKernel;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Banter.Application.Features.Users;

public record SearchUsersQuery(string SearchTerm, string? Cursor, int PageSize) : IQuery<SearchUsersResponse>;

internal class SearchUsersQueryValidator : AbstractValidator<SearchUsersQuery>
{
    public SearchUsersQueryValidator()
    {
        RuleFor(x => x.SearchTerm)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(term => !string.IsNullOrWhiteSpace(term)).WithMessage("Search term cannot be empty.")
            .Must(term => term.Trim().Length >= 2).WithMessage("Search term must be at least 2 characters.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, PaginationConstants.MaxPageSize);

        RuleFor(x => x.Cursor)
            .Must(x => x is null || !string.IsNullOrWhiteSpace(x)).WithMessage("Cursor must either be null or non-empty");
    }
}

public record SearchUsersResponse(IReadOnlyList<SearchUsersResponse.UserResponse> Users, string? NextCursor, bool HasMore)
{
    public record UserResponse(Guid Id, string DisplayName, string Email, string? ProfilePicture, bool IsOnline, bool IsBot);
}


internal class SearchUsersHandler(
    IAppDbContext _dbContext,
    IPresenceService presenceService,
    IOptions<BotOptions>? botOptions = null)
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

        // Case-insensitive search without taking an Npgsql dependency in the Application layer.
        // Npgsql's EF.Functions.ILike would be the natural PostgreSQL choice, but this query must
        // also execute against the EF in-memory provider used by the fast unit tests, where ILike
        // cannot be translated. Lower-casing both sides is provider-agnostic and Npgsql translates
        // it to `lower(column) LIKE lower(pattern)`; the PostgreSQL integration tests assert the
        // real case-insensitive behaviour.
        var searchTerm = request.SearchTerm.Trim().ToLowerInvariant();
        var includeBots = botOptions?.Value.Enabled ?? true;

        var usersQuery = _dbContext.Users.AsQueryable();

        if (!includeBots)
        {
            usersQuery = usersQuery.Where(u => !u.IsBot);
        }

        var users = await usersQuery
            .Where(u => u.DisplayName.ToLower().Contains(searchTerm)
                || (u.Email != null && u.Email.ToLower().Contains(searchTerm)))
            .Where(u => decodedCursor == null
                    || u.CreatedAt < decodedCursor.CreatedAt
                    || (u.CreatedAt == decodedCursor.CreatedAt && u.Id < decodedCursor.Id))
            .Select(u => new
            {
                u.Id,
                u.DisplayName,
                u.ProfilePictureUrl,
                u.Email,
                u.IsBot,
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
            => new SearchUsersResponse.UserResponse(user.Id, user.DisplayName, user.Email!, user.ProfilePictureUrl, IsOnline, user.IsBot))
            .ToList();

        return new SearchUsersResponse(results, nextCursor, hasMore);
    }
}
