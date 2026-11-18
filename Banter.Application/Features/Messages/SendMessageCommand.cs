using Banter.Application.Abstractions;
using Banter.Application.Abstractions.Auth;
using Banter.Application.Abstractions.Bots;
using Banter.Application.Abstractions.Data;
using Banter.Application.Abstractions.Messaging;
using Banter.Application.Abstractions.Realtime;
using Banter.Domain.Constants;
using Banter.Domain.Conversations;
using Banter.Domain.Messages;
using Banter.SharedKernel;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Banter.Application.Features.Messages;

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

internal class SendMessageCommandHandler(
    IAppDbContext _dbContext,
    IUserContext _userContext,
    ISignalRNotifier _notificationService,
    IBotReplyQueue _botReplyQueue)
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

        var participantUserIds = conversation.Participants.Select(p => p.UserId).ToList();

        var participantUsers = await _dbContext.Users
            .Where(u => participantUserIds.Contains(u.Id))
            .Select(u => new
            {
                u.Id,
                u.DisplayName,
                u.ProfilePictureUrl,
                u.IsBot
            }).ToListAsync(); // no cancellation here because the message is already sent. do you think this is correct ?

        var sender = participantUsers.FirstOrDefault(u => u.Id == userId);

        if (sender is null)
            throw new AppException("Users.NotFound", $"The user with the ID = {userId} was not found", new
            {
                userId
            });

        var notification =
            new MessageNotification(message.Id, conversation.Id, userId, message.Content, sender.DisplayName,
                sender.ProfilePictureUrl, message.CreatedAt, sender.IsBot);

        await _notificationService.SendMessageAsync(notification, otherUsersIds);   // awaiting just for safety

        // Bots only answer humans in one-to-one conversations. Group chats are deliberately
        // excluded, and a bot sender never schedules another reply (no bot-to-bot loops).
        if (!conversation.IsGroup && conversation.Participants.Count == 2 && !sender.IsBot)
        {
            var recipient = participantUsers.FirstOrDefault(u => u.Id != userId);

            if (recipient is { IsBot: true })
            {
                _botReplyQueue.TryEnqueue(new BotReplyRequest(
                    conversation.Id,
                    recipient.Id,
                    userId,
                    message.Id));
            }
        }

        return message.Id;
    }
}