using Banter.Application.Abstractions.Auth;
using Banter.Application.Abstractions.Data;
using Banter.Application.Abstractions.Messaging;
using Banter.Domain.Constants;
using Banter.Domain.Conversations;
using Banter.Domain.Messages;
using Banter.SharedKernel;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Banter.Application.Conversations;

public record SendMessageCommand(Guid ConversationId, string Content) : ICommand<Guid>;

internal class SendMessageCommandValidator : AbstractValidator<SendMessageCommand>
{
    public SendMessageCommandValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty();

        RuleFor(x => x.Content)
            .NotEmpty()
            .MaximumLength(MessageConstants.MaxLength);
    }
}

internal class SendMessageCommandHandler(IAppDbContext _context, IUserContext _userContext) : ICommandHandler<SendMessageCommand, Guid>
{
    public async Task<Result<Guid>> Handle(SendMessageCommand request, CancellationToken cancellationToken)
    {
        var userId = _userContext.UserId;

        var isParticipant = await _context.ConversationParticipants
            .AnyAsync(x => x.ConversationId == request.ConversationId && x.UserId == userId, cancellationToken);

        if (!isParticipant)
        {
            return Result<Guid>.Failure(ConversationErrors.AccessDenied);
        }


        var message = new Message()
        {
            UserId = userId,
            ConversationId = request.ConversationId,
            Content = request.Content
        };

        _context.Messages.Add(message);

        await _context.SaveChangesAsync(cancellationToken);

        return message.Id;
    }
}