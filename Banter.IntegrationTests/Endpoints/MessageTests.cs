namespace Banter.IntegrationTests.Endpoints;

public class MessageTests : IntegrationTestBase
{
    public MessageTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task SendMessage_PersistsTheRowAndUpdatesTheInboxPreview()
    {
        var ahmed = await Factory.CreateUserAsync();
        var sara = await Factory.CreateUserAsync();

        var conversationId = await CreateConversationAsync(ahmed.Client, sara.UserId);

        var response = await sara.Client.PostAsJsonAsync(
            $"/api/v1/conversations/{conversationId}/messages",
            new { Content = "  Hello Ahmed  " });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var messageId = await response.Content.ReadFromJsonAsync<Guid>();

        var message = await QueryDbAsync(database => database.Messages.SingleAsync(m => m.Id == messageId));

        message.ConversationId.Should().Be(conversationId);
        message.UserId.Should().Be(sara.UserId);
        message.Content.Should().Be("  Hello Ahmed  "); // content is stored as submitted, only validated

        var inbox = await ahmed.Client.GetFromJsonAsync<InboxResponse>("/api/v1/conversations");

        var conversation = inbox!.Conversations.Single(c => c.ConversationId == conversationId);

        conversation.LastMessageContent.Should().Be("  Hello Ahmed  ");
        conversation.UnreadCount.Should().Be(1);

        var senderView = await sara.Client.GetFromJsonAsync<MessagesResponse>(
            $"/api/v1/conversations/{conversationId}/messages");

        senderView!.LastSeenMessageId.Should().Be(messageId);
    }

    [Fact]
    public async Task SendMessage_EmptyOrWhitespaceContent_ReturnsValidationProblem()
    {
        var ahmed = await Factory.CreateUserAsync();
        var sara = await Factory.CreateUserAsync();

        var conversationId = await CreateConversationAsync(ahmed.Client, sara.UserId);

        foreach (var content in new[] { "", "   ", "\t\n" })
        {
            var response = await sara.Client.PostAsJsonAsync(
                $"/api/v1/conversations/{conversationId}/messages",
                new { Content = content });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        var messages = await QueryDbAsync(database =>
            database.Messages.CountAsync(message => message.ConversationId == conversationId));

        messages.Should().Be(0);
    }

    [Fact]
    public async Task SendMessage_ContentOverTheLimit_ReturnsValidationProblem()
    {
        var ahmed = await Factory.CreateUserAsync();
        var sara = await Factory.CreateUserAsync();

        var conversationId = await CreateConversationAsync(ahmed.Client, sara.UserId);

        var response = await sara.Client.PostAsJsonAsync(
            $"/api/v1/conversations/{conversationId}/messages",
            new { Content = new string('x', 2001) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await ReadProblemAsync(response);
        var errors = ReadErrors(problem);
        errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SendMessage_AsANonParticipant_ReturnsForbidden()
    {
        var ahmed = await Factory.CreateUserAsync();
        var sara = await Factory.CreateUserAsync();
        var outsider = await Factory.CreateUserAsync();

        var conversationId = await CreateConversationAsync(ahmed.Client, sara.UserId);

        var response = await outsider.Client.PostAsJsonAsync(
            $"/api/v1/conversations/{conversationId}/messages",
            new { Content = "I should not be here" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var problem = await ReadProblemAsync(response);
        problem.Title.Should().Be("Conversations.AccessDenied");

        var count = await QueryDbAsync(database =>
            database.Messages.CountAsync(message => message.ConversationId == conversationId));

        count.Should().Be(0);
    }

    [Fact]
    public async Task GetMessages_ReturnsNewestFirstAndPagesWithAnOpaqueCursor()
    {
        var ahmed = await Factory.CreateUserAsync();
        var sara = await Factory.CreateUserAsync();

        var conversationId = await CreateConversationAsync(ahmed.Client, sara.UserId);

        var ids = new List<Guid>();

        for (var index = 1; index <= 5; index++)
        {
            var sent = await sara.Client.PostAsJsonAsync(
                $"/api/v1/conversations/{conversationId}/messages",
                new { Content = $"Message {index}" });

            ids.Add(await sent.Content.ReadFromJsonAsync<Guid>());
        }

        var first = await ahmed.Client.GetFromJsonAsync<MessagesResponse>(
            $"/api/v1/conversations/{conversationId}/messages?pageSize=2");

        first!.Messages.Should().HaveCount(2);
        first.HasMore.Should().BeTrue();
        first.NextCursor.Should().NotBeNullOrWhiteSpace();
        first.Messages.Select(message => message.Content).Should().Equal("Message 5", "Message 4");

        var second = await ahmed.Client.GetFromJsonAsync<MessagesResponse>(
            $"/api/v1/conversations/{conversationId}/messages?pageSize=2&cursor={Uri.EscapeDataString(first.NextCursor!)}");

        second!.Messages.Select(message => message.Content).Should().Equal("Message 3", "Message 2");

        var third = await ahmed.Client.GetFromJsonAsync<MessagesResponse>(
            $"/api/v1/conversations/{conversationId}/messages?pageSize=2&cursor={Uri.EscapeDataString(second.NextCursor!)}");

        third!.Messages.Select(message => message.Content).Should().Equal("Message 1");
        third.HasMore.Should().BeFalse();
        third.NextCursor.Should().BeNull();

        var allIds = first.Messages.Concat(second.Messages).Concat(third.Messages)
            .Select(message => message.Id).ToList();

        allIds.Should().BeEquivalentTo(ids);
        allIds.Distinct().Should().HaveCount(5);
    }

    [Fact]
    public async Task GetMessages_InvalidCursorOrPageSize_ReturnsBadRequest()
    {
        var ahmed = await Factory.CreateUserAsync();
        var sara = await Factory.CreateUserAsync();

        var conversationId = await CreateConversationAsync(ahmed.Client, sara.UserId);

        var invalidCursor = await ahmed.Client.GetAsync(
            $"/api/v1/conversations/{conversationId}/messages?cursor=not-a-cursor");

        invalidCursor.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var cursorProblem = await ReadProblemAsync(invalidCursor);
        cursorProblem.Title.Should().Be("Pagination.InvalidCursor");

        var invalidPageSize = await ahmed.Client.GetAsync(
            $"/api/v1/conversations/{conversationId}/messages?pageSize=0");

        invalidPageSize.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetMessages_AsANonParticipant_ReturnsForbidden()
    {
        var ahmed = await Factory.CreateUserAsync();
        var sara = await Factory.CreateUserAsync();
        var outsider = await Factory.CreateUserAsync();

        var conversationId = await CreateConversationAsync(ahmed.Client, sara.UserId);

        var response = await outsider.Client.GetAsync($"/api/v1/conversations/{conversationId}/messages");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<Guid> CreateConversationAsync(HttpClient client, Guid participantId)
    {
        var response = await client.PostAsJsonAsync("/api/v1/conversations", new
        {
            ParticipantsIds = new[] { participantId },
            GroupTitle = (string?)null
        });

        return await ReadGuidAsync(response);
    }

    private sealed record InboxResponse(
        IReadOnlyList<InboxConversation> Conversations,
        string? Nextcursor,
        bool HasMore);

    private sealed record InboxConversation(
        Guid ConversationId,
        string DisplayName,
        List<string?> ProfilePictures,
        string? LastMessageContent,
        DateTime? LastMessageCreatedAt,
        bool IsGroup,
        bool IsOnline,
        int UnreadCount,
        bool IsBot);

    private sealed record MessagesResponse(
        IReadOnlyList<MessageDto> Messages,
        string? NextCursor,
        bool HasMore,
        Guid? LastSeenMessageId);

    private sealed record MessageDto(
        Guid Id,
        string Content,
        DateTime CreatedAt,
        Guid UserId,
        string DisplayName,
        string? ProfilePictureUrl,
        bool IsBot);
}
