using Banter.Application.Abstractions;
using Banter.Application.Abstractions.Auth;
using Banter.Application.Abstractions.Data;
using Banter.Application.Abstractions.Messaging;
using Banter.Application.Common;
using Banter.Application.Constants;
using Banter.Application.Errors;
using Banter.SharedKernel;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Banter.Application.Conversations;

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
    bool IsOnline);
}

internal record GetInboxQuery(string? Cursor, int PageSize) : IQuery<GetInboxResponse>;

internal class GetInboxQueryValidator : AbstractValidator<GetInboxQuery>
{
    public GetInboxQueryValidator()
    {
        RuleFor(x => x.PageSize)
             .InclusiveBetween(1, PaginationConstants.MaxPageSize);
    }
}

internal class GetInboxHandler(IAppDbContext _dbContext, IUserContext _userContext,IPresenceService _presenceService)
    : IQueryHandler<GetInboxQuery, GetInboxResponse>
{
    public async Task<Result<GetInboxResponse>> Handle(GetInboxQuery request, CancellationToken cancellationToken)
    {
        Guid userId = _userContext.UserId;

        PageCursor? decodedCursor = null;

        if (request.Cursor is not null)
        {
            decodedCursor = PageCursor.Decode(request.Cursor);

            if (decodedCursor is null)
                return Result.Failure<GetInboxResponse>(PageErrors.InvalidCursor);
        }

        var query =
            from p in _dbContext.ConversationParticipants
            join c in _dbContext.Conversations
                on p.ConversationId equals c.Id
            join lm in _dbContext.Messages
                on c.LastMessageId equals lm.Id
            where p.UserId == userId
            where decodedCursor == null
               || lm.CreatedAt < decodedCursor.CreatedAt
               || (lm.CreatedAt == decodedCursor.CreatedAt && lm.Id < decodedCursor.Id)
            orderby lm.CreatedAt descending, c.Id descending
            select new
            {
                ConversationId = c.Id,
                c.Title,
                c.IsGroup,
                LastMessageContent = lm.Content,
                LastMessageCreatedAt = lm.CreatedAt,
                lm.CreatedAt,
                lm.Id
            };

        var conversations = await query
                .Take(request.PageSize + 1)
                .ToListAsync(cancellationToken);

        var hasMore = conversations.Count > request.PageSize;

        if (hasMore)
        {
            conversations.RemoveAt(conversations.Count - 1);
        }

        var conversationIds = conversations.Select(c => c.ConversationId).ToList();

        var lastConversation = conversations.LastOrDefault();

        var nextCursor = hasMore && lastConversation is not null ?
            PageCursor.Encode(lastConversation.CreatedAt, lastConversation.Id)
            : null;

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
                u.ProfilePictureUrl
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
            c.Conversation.Title ?? string.Join(", " ,c.Participants.Select(p => p.DisplayName)), // domain doesn't allow for a group conversation to 
            c.Participants.Select(p => p.ProfilePictureUrl).ToList(),                             // not have a title so slightly redundant.
            c.Conversation.LastMessageContent,
            c.Conversation.LastMessageCreatedAt,
            c.Conversation.IsGroup,
            s))
            .ToList();

        return new GetInboxResponse(result, nextCursor, hasMore);
    }
}