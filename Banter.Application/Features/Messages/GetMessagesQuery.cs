using Banter.Application.Abstractions.Auth;
using Banter.Application.Abstractions.Data;
using Banter.Application.Abstractions.Messaging;
using Banter.Application.Constants;
using Banter.Application.Errors;
using Banter.Application.Extensions;
using Banter.Application.Features.Common;
using Banter.Domain.Conversations;
using Banter.SharedKernel;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Banter.Application.Features.Messages;

public record GetMessagesQuery(Guid ConversationId, string? Cursor, int PageSize) : IQuery<GetMessagesResponse>;
public record GetMessagesResponse(IReadOnlyList<GetMessagesResponse.MessageResponse> messages, string? NextCursor, bool HasMore, Guid? LastSeenMessageId)
{
    public record MessageResponse(Guid Id, string Content, DateTime CreatedAt, Guid UserId, string DisplayName, string? ProfilePictureUrl);
}

internal class GetMessagesQueryValidator : AbstractValidator<GetMessagesQuery>
{
    public GetMessagesQueryValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty();

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, PaginationConstants.MaxPageSize);

        RuleFor(x => x.Cursor)
            .Must(x => x is null || !string.IsNullOrWhiteSpace(x)).WithMessage("Cursor must either be null or non-empty");
    }
}

internal class GetMessagesQueryHandler(IAppDbContext _dbContext, IUserContext _userContext)
    : IQueryHandler<GetMessagesQuery, GetMessagesResponse>
{
    public async Task<Result<GetMessagesResponse>> Handle(GetMessagesQuery request, CancellationToken cancellationToken)
    {

        var userId = _userContext.UserId;

        var participant = await _dbContext.ConversationParticipants
            .Where(p => p.UserId == userId && p.ConversationId == request.ConversationId)
            .Select(p => new
            {
                Exists = true,
                p.LastSeenMessageId,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (participant is null)
            return Result.Failure<GetMessagesResponse>(ConversationErrors.AccessDenied(request.ConversationId));

        PageCursor? decodedCursor = null;

        if (request.Cursor is not null)
        {
            decodedCursor = PageCursor.Decode(request.Cursor);

            if (decodedCursor is null)
                return Result.Failure<GetMessagesResponse>(PageErrors.InvalidCursor);
        }

        var query = from m in _dbContext.Messages
                    join u in _dbContext.Users on m.UserId equals u.Id
                    where m.ConversationId == request.ConversationId
                    where decodedCursor == null
                    || m.CreatedAt < decodedCursor.CreatedAt
                    || (m.CreatedAt == decodedCursor.CreatedAt && m.Id < decodedCursor.Id) // for postgres using the method lessthanorequal is faster.
                    orderby m.CreatedAt descending, m.Id descending                        // if sequental guids are used which they are be default in newer  
                    select new GetMessagesResponse.MessageResponse(                        // versions then we can just sort by ids only.
                        m.Id,
                        m.Content,
                        m.CreatedAt,
                        m.UserId,
                        u.DisplayName,
                        u.ProfilePictureUrl);

        var messages = await query
            .Take(request.PageSize + 1)
            .ToListAsync(cancellationToken);

        var hasMore = PaginationHelpers.Slice(messages, request.PageSize);

        var nextCursor = PaginationHelpers.CreateNextCursor(messages, hasMore, x => x.CreatedAt, x => x.Id);

        var lastSeenMessageId = participant.LastSeenMessageId;

        return new GetMessagesResponse(messages, nextCursor, hasMore, lastSeenMessageId);
    }
}