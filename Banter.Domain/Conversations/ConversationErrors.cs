using Banter.SharedKernel;

namespace Banter.Domain.Conversations;

public static class ConversationErrors
{
    public static Error AccessDenied = Error.Forbidden("Conversations.AccessDenied", "You are not a member of this conversation.");
}

