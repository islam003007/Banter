using Banter.Application.Abstractions.Auth;
using Banter.Application.Abstractions.Data;
using Banter.Application.Abstractions.Messaging;
using Banter.Domain.Conversations;
using Banter.SharedKernel;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Banter.Application.Conversations;

public record CreateConversationCommand(Guid UserId) : ICommand<Guid>;

internal class CreateConversationCommandValidator : AbstractValidator<CreateConversationCommand>
{
    public CreateConversationCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty();
    }
}

internal class CreateConversationCommandHandler(IAppDbContext _dbContext, IUserContext _userContext) : ICommandHandler<CreateConversationCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateConversationCommand request, CancellationToken cancellationToken)
    {
        Guid currentUserId = _userContext.UserId;

        Guid oldConversationId = await _dbContext.Conversations.Where(c => c.Participants.Count == 2
        && c.Participants.Any(p => p.UserId == currentUserId)
        && c.Participants.Any(p => p.UserId == request.UserId))
            .Select(c => c.Id)
            .FirstOrDefaultAsync();

        if (oldConversationId != default)
            return oldConversationId;

        Conversation conversation = Conversation.CreateDM(currentUserId, request.UserId);

        await _dbContext.Conversations.AddAsync(conversation);

        await _dbContext.SaveChangesAsync();

        return conversation.Id;
    }
}