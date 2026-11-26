using Banter.Application.Abstractions;
using Banter.Application.Abstractions.Auth;
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

namespace Banter.Application.Features.Conversations;

public record GetInboxResponse(IReadOnlyList<GetInboxResponse.ConversationResponse> Conversations,
    string? Nextcursor,
    bool HasMore)
{
    public record ConversationResponse(Guid ConversationId,
    string DisplayName,
    List<string> profilePictures,
    string? LastMessageContent,
    DateTime? LastMessageCreatedAt,
    bool IsGroup,
    bool IsOnline,
    int UnreadCount,
    bool IsBot);
}

public record GetInboxQuery(string? Cursor, int PageSize) : IQuery<GetInboxResponse>;

internal class GetInboxQueryValidator : AbstractValidator<GetInboxQuery>
{
    public GetInboxQueryValidator()
    {
        RuleFor(x => x.PageSize)
             .InclusiveBetween(1, PaginationConstants.MaxPageSize);

        RuleFor(x => x.Cursor)
            .Must(x => x is null || !string.IsNullOrWhiteSpace(x)).WithMessage("Cursor must either be null or non-empty");
    }
}

internal class GetInboxHandler(
    IAppDbContext _dbContext,
    IUserContext _userContext,
    IPresenceService _presenceService,
    IOptions<BotOptions>? botOptions = null)
    : IQueryHandler<GetInboxQuery, GetInboxResponse>
{
    public async Task<Result<GetInboxResponse>> Handle(GetInboxQuery request, CancellationToken cancellationToken)
    {
        Guid userId = _userContext.UserId;
        var includeBots = botOptions?.Value.Enabled ?? true;

        PageCursor? decodedCursor = null;

        if (request.Cursor is not null)
        {
            decodedCursor = PageCursor.Decode(request.Cursor);

            if (decodedCursor is null)
                return Result.Failure<GetInboxResponse>(PageErrors.InvalidCursor);
        }

        // Left join the last message so conversations without any messages still show up.
        // Empty conversations are ordered by their creation time, which keeps cursor paging stable.
        var query =
            from p in _dbContext.ConversationParticipants
            join c in _dbContext.Conversations
                on p.ConversationId equals c.Id
            join lm in _dbContext.Messages
                on c.LastMessageId equals lm.Id into lastMessages
            from lm in lastMessages.DefaultIfEmpty()
            join sm in _dbContext.Messages
                on p.LastSeenMessageId equals sm.Id into seenMessages
            from sm in seenMessages.DefaultIfEmpty()
            where p.UserId == userId
            where includeBots || !c.Participants.Any(cp => cp.User.IsBot)
            let activityAt = lm != null ? lm.CreatedAt : c.CreatedAt
            let activityId = lm != null ? lm.Id : c.Id
            where decodedCursor == null
               || activityAt < decodedCursor.CreatedAt
               || (activityAt == decodedCursor.CreatedAt && activityId < decodedCursor.Id)
            orderby activityAt descending, activityId descending
            select new
            {
                ConversationId = c.Id,
                c.Title,
                c.IsGroup,
                LastMessageContent = lm != null ? lm.Content : null,
                LastMessageCreatedAt = lm != null ? (DateTime?)lm.CreatedAt : null,
                UnreadCount = _dbContext.Messages.Count(m =>
                    m.ConversationId == c.Id
                    && m.UserId != userId
                    && (sm == null
                        || m.CreatedAt > sm.CreatedAt
                        || (m.CreatedAt == sm.CreatedAt && m.Id > sm.Id))),
                ActivityAt = activityAt,
                ActivityId = activityId
            };

        var conversations = await query
                .Take(request.PageSize + 1)
                .ToListAsync(cancellationToken);

        var hasMore = PaginationHelpers.Slice(conversations, request.PageSize);

        var nextCursor = PaginationHelpers.CreateNextCursor(conversations, hasMore, x => x.ActivityAt, x => x.ActivityId);

        var conversationIds = conversations.Select(c => c.ConversationId).ToList();

        var participantsQuery =
            from p in _dbContext.ConversationParticipants
            where conversationIds.Contains(p.ConversationId)
                && p.UserId != userId
            join u in _dbContext.Users
                on p.UserId equals u.Id
            select new
            {
                p.ConversationId,
                p.UserId,
                u.DisplayName,
                u.ProfilePictureUrl,
                u.IsBot
            };

        var participants = await participantsQuery
            .ToListAsync(cancellationToken);

        var enrichedConversations = conversations.GroupJoin(participants, c => c.ConversationId, p => p.ConversationId, (c, p) => new
        {
            Conversation = c,
            Participants = p,
        });

        var isOnlineStatuses = await Task.WhenAll(enrichedConversations
            .Select(c => c.Conversation.IsGroup ? Task.FromResult(false)
            : _presenceService.IsOnlineAsync(c.Participants.Select(p => p.UserId).First(), cancellationToken)));

        var result = enrichedConversations
            .Zip(isOnlineStatuses, (c, s) =>
            new GetInboxResponse.ConversationResponse(c.Conversation.ConversationId,
            c.Conversation.Title ?? string.Join(", ", c.Participants.Select(p => p.DisplayName)), // domain doesn't allow for a group conversation to
            c.Participants.Select(p => p.ProfilePictureUrl).ToList(),                             // not have a title so slightly redundant.
            c.Conversation.LastMessageContent,
            c.Conversation.LastMessageCreatedAt,
            c.Conversation.IsGroup,
            s,
            c.Conversation.UnreadCount,
            !c.Conversation.IsGroup && c.Participants.Any(p => p.IsBot)))
            .ToList();

        return new GetInboxResponse(result, nextCursor, hasMore);
    }
}
