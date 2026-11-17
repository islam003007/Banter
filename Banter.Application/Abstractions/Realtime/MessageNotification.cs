namespace Banter.Application.Abstractions.Realtime;

public record MessageNotification(Guid Id,
    Guid ConversationId,
    Guid UserId,
    string Content,
    string SenderDisplayName,
    string? SenderProfilePictureUrl,
    DateTime CreatedAt);
