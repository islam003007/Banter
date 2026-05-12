using Banter.Application.Abstractions;
using Banter.Application.Abstractions.Auth;
using Banter.Application.Abstractions.Data;
using Banter.Application.Abstractions.Messaging;
using Banter.Application.Constants;
using Banter.Application.Extensions;
using Banter.SharedKernel;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Banter.Application.Conversations;

public record GetInboxResponse(Guid ConversationId,
    string DisplayName,
    List<string> profilePictures, 
    string? LastMessageContent,
    DateTime? LastMessageCreatedAt,
    bool IsGroup,
    bool IsOnline);

internal record GetInboxQuery(int PageSize, int PageNumber) : IQuery<IReadOnlyList<GetInboxResponse>>, IPagedQuery;

internal class GetInboxQueryValidator : AbstractValidator<GetInboxQuery>
{
    public GetInboxQueryValidator()
    {
        RuleFor(x => x)
            .HasValidPagination();
    }
}

internal class GetInboxHandler(IAppDbContext _dbContext, IUserContext _userContext,IPresenceService _presenceService)
    : IQueryHandler<GetInboxQuery, IReadOnlyList<GetInboxResponse>>
{
    public async Task<Result<IReadOnlyList<GetInboxResponse>>> Handle(GetInboxQuery request, CancellationToken cancellationToken)
    {
        Guid userId = _userContext.UserId;

        var Conversations = await 
            (
                from p in _dbContext.ConversationParticipants
                join ls in _dbContext.Messages 
                    on p.LastMessageId equals ls.Id into messageGroup
                from ls in messageGroup.DefaultIfEmpty()
                where p.UserId == userId 
                orderby ls.CreatedAt descending
                select new
                {
                    ConversationId = p.Conversation.Id,
                    p.Conversation.Title,
                    LastMessageContent = (string?)ls.Content,
                    LastMessageCreatedAt = (DateTime?)ls.CreatedAt,
                    p.Conversation.IsGroup,
                }
            )
            .Skip(request.PageSize * (request.PageNumber - 1))
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var participants = await 
            (
                from p in _dbContext.ConversationParticipants
                where Conversations.Select(c => c.ConversationId).Contains(p.Conversation.Id)
                    && p.UserId != userId
                join u in _dbContext.Users
                    on p.UserId equals u.Id
                select new
                {
                   p.ConversationId,
                   p.UserId,
                   u.DisplayName,
                   u.ProfilePictureUrl
                }
            )
            .ToListAsync(cancellationToken);

        var enrichedConversations = Conversations.GroupJoin(participants, c => c.ConversationId, p => p.ConversationId, (c, p) => new
        {
            Conversation = c,
            Participants = p,
        });


        var isOnlineStatuses = await Task.WhenAll(enrichedConversations
            .Select(c => c.Conversation.IsGroup ? Task.FromResult(false)
            : _presenceService.IsOnlineAsync(c.Participants.Select(p => p.UserId).First(), cancellationToken)));


        var result = enrichedConversations
            .Zip(isOnlineStatuses, (c, s) => 
            new GetInboxResponse(c.Conversation.ConversationId,
            c.Conversation.Title ?? string.Join(", " ,c.Participants.Select(p => p.DisplayName)), // domain doesn't allow for a group conversation to 
            c.Participants.Select(p => p.ProfilePictureUrl).ToList(),                             // not have a title so slightly redundant.
            c.Conversation.LastMessageContent,
            c.Conversation.LastMessageCreatedAt,
            c.Conversation.IsGroup,
            s))
            .ToList();

        return result;
    }
}