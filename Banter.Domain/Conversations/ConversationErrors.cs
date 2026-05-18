using Banter.SharedKernel;

namespace Banter.Domain.Conversations;

public static class ConversationErrors
{
    public static Error AccessDenied(Guid conversationId)
        => Error.Forbidden("Conversations.AccessDenied", $"You are not a member of the conversation with ID = {conversationId} to access it");

    public static Error CreatorCannotBeParticipant =
        Error.Conflict("Conversations.CreatorCannotBeParticipant", "The conversation creator cannot also appear in the participants list.");
}