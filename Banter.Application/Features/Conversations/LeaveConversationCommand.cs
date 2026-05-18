using Banter.Application.Abstractions.Auth;
using Banter.Application.Abstractions.Data;
using Banter.Application.Abstractions.Messaging;
using Banter.Domain.Conversations;
using Banter.SharedKernel;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Banter.Application.Features.Conversations;

public record LeaveConversationCommand(Guid ConverstaionId) : ICommand;

internal class LeaveConversationCommandValidator : AbstractValidator<LeaveConversationCommand>
{
    public LeaveConversationCommandValidator()
    {
        RuleFor(x => x.ConverstaionId)
            .NotEmpty();
    }
}

internal class LeaveConversationCommandHandler(IAppDbContext _dbContext, IUserContext _userContext) : ICommandHandler<LeaveConversationCommand>
{
    public async Task<Result> Handle(LeaveConversationCommand request, CancellationToken cancellationToken)
    {
        var userId = _userContext.UserId;

        var participant = await _dbContext.ConversationParticipants
            .Where(p => p.UserId == userId && p.ConversationId == request.ConverstaionId)
            .Include(p => p.Conversation)
            .FirstOrDefaultAsync(cancellationToken);

        if (participant is null)
            return Result.Failure(ConversationErrors.AccessDenied(request.ConverstaionId));

        if (!participant.Conversation.IsGroup)
            return Result.Success(); // TODO: Upgrade this if needed.

        _dbContext.ConversationParticipants.Remove(participant);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}