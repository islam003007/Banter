using Banter.Application.Abstractions.Auth;
using Banter.Application.Abstractions.Data;
using Banter.Application.Abstractions.Messaging;
using Banter.Domain.Conversations;
using Banter.SharedKernel;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Banter.Application.Features.Conversations;

public record MarkConversationAsReadCommand(Guid ConversationId) : ICommand;

internal class MarkConversationAsReadCommandValidator : AbstractValidator<MarkConversationAsReadCommand>
{
    public MarkConversationAsReadCommandValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty();
    }
}

internal class MarkConversationAsReadCommandHandler(IAppDbContext _dbContext, IUserContext _userContext) : ICommandHandler<MarkConversationAsReadCommand>
{
    public async Task<Result> Handle(MarkConversationAsReadCommand request, CancellationToken cancellationToken)
    {
        var userId = _userContext.UserId;

        var participant = await _dbContext.ConversationParticipants
            .Include(x => x.Conversation)
            .Where(p => p.ConversationId == request.ConversationId && p.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        if (participant is null)
            return Result.Failure(ConversationErrors.AccessDenied(request.ConversationId));

        participant.LastSeenMessageId = participant.Conversation.LastMessageId;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}