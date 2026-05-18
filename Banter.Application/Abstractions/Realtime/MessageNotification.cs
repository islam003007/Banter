namespace Banter.Application.Abstractions.Realtime;

public record MessageNotification(Guid Id,
    Guid ConversationId,
    Guid UserId,
    string senderDisplayName,
    string? SenderProfilePictureUrl,
    DateTime CreatedAt);