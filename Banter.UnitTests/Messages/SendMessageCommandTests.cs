using Banter.Application.Abstractions.Realtime;
using Banter.Application.Features.Messages;
using Banter.Domain.Conversations;
using Banter.UnitTests.TestSupport;
using Xunit;

namespace Banter.UnitTests.Messages;

public class SendMessageCommandTests
{
    [Fact]
    public async Task Handle_PublishesTheSavedMessageWithItsContent()
    {
        await using var database = TestDatabase.Create();

        var ahmed = database.AddUser("ahmed@example.com", "Ahmed");
        var sara = database.AddUser("sara@example.com", "Sara");

        await database.SaveAsync();

        var conversation = Conversation.CreateDM(ahmed.Id, sara.Id);
        database.Context.Conversations.Add(conversation);

        await database.SaveAsync();

        var notifier = new CapturingNotifier();

        var handler = new SendMessageCommandHandler(
            database.Context,
            new FakeUserContext(ahmed.Id),
            notifier);

        var result = await handler.Handle(
            new SendMessageCommand(conversation.Id, "Hello Sara"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        var notification = Assert.IsType<MessageNotification>(notifier.Notification);

        Assert.Equal(result.Value, notification.Id);
        Assert.Equal(conversation.Id, notification.ConversationId);
        Assert.Equal(ahmed.Id, notification.UserId);
        Assert.Equal("Hello Sara", notification.Content);
        Assert.Equal("Ahmed", notification.SenderDisplayName);
        Assert.Equal(new[] { sara.Id.ToString() }, notifier.UserIds);
    }

    private sealed class CapturingNotifier : ISignalRNotifier
    {
        public MessageNotification? Notification { get; private set; }
        public IReadOnlyList<string> UserIds { get; private set; } = [];

        public Task SendMessageAsync(MessageNotification message, IReadOnlyList<string> userIds)
        {
            Notification = message;
            UserIds = userIds;

            return Task.CompletedTask;
        }
    }
}
