using Banter.SharedKernel;

namespace Banter.Domain.Conversations;

public static class ConversationErrors
{
    public static Error AccessDenied(Guid conversationId)
        => Error.Forbidden("Conversations.AccessDenied", $"You are not a member of the conversation with ID = {conversationId} to access it");

    public static Error NotFound(Guid conversationId)
        => Error.NotFound("Conversations.NotFound", $"The conversation with the ID = {conversationId} was not found");

    public static readonly Error CreatorCannotBeParticipant =
        Error.Conflict("Conversations.CreatorCannotBeParticipant", "The conversation creator cannot also appear in the participants list.");

    public static readonly Error ParticipantsRequired =
        Error.Problem("Conversations.ParticipantsRequired", "At least one participant is required.");

    public static Error ParticipantsNotFound(IReadOnlyCollection<Guid> userIds)
        => Error.NotFound(
            "Conversations.ParticipantsNotFound",
            $"These participants do not exist: {string.Join(", ", userIds)}");

    public static readonly Error TitleRequired =
        Error.Problem("Conversations.TitleRequired", "A group conversation must have a title.");

    public static readonly Error TitleTooLong =
        Error.Problem("Conversations.TitleTooLong", "The conversation title is too long.");

    public static readonly Error TooManyParticipants =
        Error.Problem("Conversations.TooManyParticipants", "The conversation has too many participants.");

    public static readonly Error BotsDisabled =
        Error.Problem("Conversations.BotsDisabled", "Bot conversations are disabled.");
}
