using Banter.Application.Abstractions.Ai;
using Banter.Application.Abstractions.Bots;
using Banter.Application.Abstractions.Data;
using Banter.Application.Abstractions.Realtime;
using Banter.Domain.Constants;
using Banter.Domain.Messages;
using Microsoft.EntityFrameworkCore;

namespace Banter.Application.Bots;

/// <summary>
/// Persists and broadcasts one bot reply.
///
/// Scope rule: bots answer only in one-to-one conversations and only when the triggering message
/// was written by a human. Group conversations never receive bot replies, and a message from a bot
/// never schedules another reply, so bot-to-bot loops are impossible by construction.
/// </summary>
internal sealed class BotReplyProcessor(
    IAppDbContext _dbContext,
    ISignalRNotifier _notifier,
    IAiChatClient _aiChatClient)
    : IBotReplyProcessor
{
    private const int HistoryLimit = 12;

    public async Task ProcessAsync(BotReplyRequest request, CancellationToken cancellationToken = default)
    {
        var conversation = await _dbContext.Conversations
            .Include(c => c.Participants)
            .ThenInclude(p => p.User)
            .FirstOrDefaultAsync(c => c.Id == request.ConversationId, cancellationToken);

        if (conversation is null || conversation.IsGroup || conversation.Participants.Count != 2)
            return;

        var bot = conversation.Participants
            .FirstOrDefault(p => p.UserId == request.BotUserId && p.User.IsBot);

        var human = conversation.Participants
            .FirstOrDefault(p => p.UserId == request.HumanUserId && !p.User.IsBot);

        if (bot is null || human is null)
            return;

        // Only answer the newest human turn. If the queue is behind and the human already sent
        // something newer, that newer message has its own queue entry; answering both would make
        // the bot look like it is talking to itself.
        var latestMessage = await _dbContext.Messages
            .Where(m => m.ConversationId == conversation.Id)
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Select(m => new { m.Id, m.UserId })
            .FirstOrDefaultAsync(cancellationToken);

        if (latestMessage is null
            || latestMessage.Id != request.TriggeringMessageId
            || latestMessage.UserId != human.UserId)
            return;

        await _notifier.SendBotTypingAsync(
            conversation.Id,
            bot.User.DisplayName,
            [human.UserId.ToString()]);

        var history = await (
            from m in _dbContext.Messages
            join u in _dbContext.Users on m.UserId equals u.Id
            where m.ConversationId == conversation.Id
            orderby m.CreatedAt descending, m.Id descending
            select new BotChatMessage(
                m.Id,
                m.UserId,
                u.DisplayName,
                u.IsBot,
                m.Content,
                m.CreatedAt))
            .Take(HistoryLimit)
            .ToListAsync(cancellationToken);

        history.Reverse();

        var reply = await _aiChatClient.GenerateReplyAsync(
            new BotChatRequest(
                conversation.Id,
                bot.UserId,
                bot.User.Email ?? string.Empty,
                bot.User.DisplayName,
                human.UserId,
                human.User.DisplayName,
                history),
            cancellationToken);

        var content = reply?.Trim();

        if (string.IsNullOrWhiteSpace(content))
            return;

        if (content.Length > MessageConstants.MaxLength)
            content = content[..MessageConstants.MaxLength];

        var message = new Message(conversation.Id, bot.UserId, content);

        _dbContext.Messages.Add(message);

        conversation.LastMessageId = message.Id;
        bot.LastSeenMessageId = message.Id;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var notification = new MessageNotification(
            message.Id,
            conversation.Id,
            bot.UserId,
            message.Content,
            bot.User.DisplayName,
            bot.User.ProfilePictureUrl,
            message.CreatedAt,
            IsBot: true);

        await _notifier.SendMessageAsync(notification, [human.UserId.ToString()]);
    }
}
