using Banter.Application.Abstractions;
using Banter.Application.Abstractions.Auth;
using Banter.Application.Abstractions.Data;
using Banter.Application.Abstractions.Messaging;
using Banter.Domain.Conversations;
using Banter.SharedKernel;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Banter.Application.Features.Conversations;

public record GetConversationQuery(Guid ConversationId) : IQuery<GetConversationResponse>;

public record GetConversationResponse(
    Guid ConversationId,
    string? Title,
    bool IsGroup,
    IReadOnlyList<GetConversationResponse.ParticipantResponse> Participants)
{
    public record ParticipantResponse(Guid UserId, string DisplayName, string? ProfilePictureUrl, bool IsOnline, bool IsBot);
}

internal class GetConversationQueryValidator : AbstractValidator<GetConversationQuery>
{
    public GetConversationQueryValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty();
    }
}

internal class GetConversationQueryHandler(
    IAppDbContext _dbContext,
    IUserContext _userContext,
    IPresenceService _presenceService)
    : IQueryHandler<GetConversationQuery, GetConversationResponse>
{
    public async Task<Result<GetConversationResponse>> Handle(GetConversationQuery request, CancellationToken cancellationToken)
    {
        var userId = _userContext.UserId;

        var conversation = await _dbContext.Conversations
            .Where(c => c.Id == request.ConversationId)
            .Select(c => new
            {
                c.Id,
                c.Title,
                c.IsGroup
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (conversation is null)
            return Result.Failure<GetConversationResponse>(ConversationErrors.NotFound(request.ConversationId));

        var isParticipant = await _dbContext.ConversationParticipants
            .AnyAsync(p => p.ConversationId == request.ConversationId && p.UserId == userId, cancellationToken);

        if (!isParticipant)
            return Result.Failure<GetConversationResponse>(ConversationErrors.AccessDenied(request.ConversationId));

        var participants = await (
            from p in _dbContext.ConversationParticipants
            join u in _dbContext.Users
                on p.UserId equals u.Id
            where p.ConversationId == request.ConversationId
            orderby u.DisplayName
            select new
            {
                p.UserId,
                u.DisplayName,
                u.ProfilePictureUrl,
                u.IsBot
            }).ToListAsync(cancellationToken);

        var onlineStatuses = await Task.WhenAll(participants
            .Select(p => _presenceService.IsOnlineAsync(p.UserId, cancellationToken)));

        var participantResponses = participants
            .Zip(onlineStatuses, (participant, isOnline) =>
                new GetConversationResponse.ParticipantResponse(
                    participant.UserId,
                    participant.DisplayName,
                    participant.ProfilePictureUrl,
                    isOnline,
                    participant.IsBot))
            .ToList();

        return new GetConversationResponse(
            conversation.Id,
            conversation.Title,
            conversation.IsGroup,
            participantResponses);
    }
}
