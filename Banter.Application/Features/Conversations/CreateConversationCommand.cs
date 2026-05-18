using Banter.Application.Abstractions.Auth;
using Banter.Application.Abstractions.Data;
using Banter.Application.Abstractions.Messaging;
using Banter.Domain.Conversations;
using Banter.SharedKernel;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Banter.Application.Features.Conversations;

public record CreateConversationCommand(List<Guid> ParticipantIds, string? GroupTitle = null) : ICommand<Guid>;

internal class CreateConversationCommandValidator : AbstractValidator<CreateConversationCommand>
{
    public CreateConversationCommandValidator()
    {
        RuleFor(x => x.ParticipantIds)
            .NotEmpty()
            .Must(x => x.Count >= 1).WithMessage("At least 1 ParticipantId is required.");

        RuleFor(x => x.GroupTitle)
            .NotEmpty().When(x => x.ParticipantIds.Count > 1).WithMessage("A group conversation must have a title");
    }
}

internal class CreateConversationCommandHandler(IAppDbContext _dbContext, IUserContext _userContext) : ICommandHandler<CreateConversationCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateConversationCommand request, CancellationToken cancellationToken)
    {
        Guid currentUserId = _userContext.UserId;

        var participants = request.ParticipantIds.Distinct().ToList();

        if (request.ParticipantIds.Contains(currentUserId))
            return Result.Failure<Guid>(ConversationErrors.CreatorCannotBeParticipant);

        Conversation? conversation = null;

        if (participants.Count == 1)
        {
            var otherUserId = request.ParticipantIds.First();

            Guid oldConversationId = await _dbContext.Conversations.Where(c => c.Participants.Count == 2
                && c.Participants.Any(p => p.UserId == currentUserId)
                && c.Participants.Any(p => p.UserId == otherUserId))
            .Select(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);

            if (oldConversationId != default)
                return oldConversationId;

            conversation = Conversation.CreateDM(currentUserId, otherUserId);
        }
        else
        {
            conversation = Conversation.CreateGroupConversation(participants, request.GroupTitle!);
        }

        await _dbContext.Conversations.AddAsync(conversation, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return conversation.Id;
    }
}