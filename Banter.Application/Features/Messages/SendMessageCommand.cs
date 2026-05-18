using Banter.Application.Abstractions;
using Banter.Application.Abstractions.Auth;
using Banter.Application.Abstractions.Data;
using Banter.Application.Abstractions.Messaging;
using Banter.Application.Abstractions.Realtime;
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

internal class SendMessageCommandHandler(IAppDbContext _dbContext, IUserContext _userContext, INotificationService _notificationService)
    : ICommandHandler<SendMessageCommand, Guid>
{
    public async Task<Result<Guid>> Handle(SendMessageCommand request, CancellationToken cancellationToken)
    {
        var userId = _userContext.UserId;

        var isParticipant = await _dbContext.ConversationParticipants
            .AnyAsync(x => x.ConversationId == request.ConversationId && x.UserId == userId, cancellationToken);

        if (!isParticipant)
        {
            return Result<Guid>.Failure(ConversationErrors.AccessDenied(request.ConversationId));
        }

        var message = new Message(request.ConversationId, userId, request.Content);

        _dbContext.Messages.Add(message);

        // update last message id

        var conversation = await _dbContext.Conversations
            .Include(c => c.Participants)
            .Where(c => request.ConversationId == c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (conversation is null)                                                        // this signals database inconsitency not a simple 404.
            throw new AppException("Conversations.NotFound",
                $"The conversation with the ID = {request.ConversationId} was not found",
                new
                {
                    request.ConversationId,
                });


        conversation.LastMessageId = message.Id;

        var participant = conversation.Participants.Where(p => p.UserId == userId).First();

        participant.LastSeenMessageId = message.Id;

        await _dbContext.SaveChangesAsync(cancellationToken);

        // send notification

        List<string> otherUsersIds = conversation.Participants.Where(p => p.UserId != userId).Select(p => p.UserId.ToString()).ToList();

        var user = await _dbContext.Users
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.DisplayName,
                u.ProfilePictureUrl
            }).FirstOrDefaultAsync(); // no cancellation here because the message is already sent. do you think this is correct ?

        if (user is null)
            throw new AppException("Users.NotFound", $"The user with the ID = {userId} was not found", new
            {
                userId
            });

        var notification =
            new MessageNotification(Guid.CreateVersion7(), conversation.Id, userId, user.DisplayName, user.ProfilePictureUrl, DateTime.UtcNow);

        await _notificationService.SendMessageAsync(notification, otherUsersIds);   // awaiting just for safety

        return message.Id;
    }
}