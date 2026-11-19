using Banter.Domain.Constants;

namespace Banter.IntegrationTests.Endpoints;

public class ConversationTests : IntegrationTestBase
{
    public ConversationTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task CreateDirectConversation_IncludesBothUsersAndReusesTheExistingThread()
    {
        var ahmed = await Factory.CreateUserAsync();
        var sara = await Factory.CreateUserAsync();

        var first = await CreateConversationAsync(ahmed.Client, [sara.UserId]);
        var second = await CreateConversationAsync(ahmed.Client, [sara.UserId]);

        second.Should().Be(first);

        var detail = await ahmed.Client.GetFromJsonAsync<ConversationDetail>($"/api/v1/conversations/{first}");

        detail.Should().NotBeNull();
        detail!.IsGroup.Should().BeFalse();
        detail.Title.Should().BeNull();
        detail.Participants.Select(participant => participant.UserId)
            .Should().BeEquivalentTo([ahmed.UserId, sara.UserId]);

        using var saraClient = sara.Client;
    }

    [Fact]
    public async Task CreateDirectConversation_WithTheCreatorAsParticipant_ReturnsConflict()
    {
        var ahmed = await Factory.CreateUserAsync();

        var response = await ahmed.Client.PostAsJsonAsync("/api/v1/conversations", new
        {
            ParticipantsIds = new[] { ahmed.UserId },
            GroupTitle = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await ReadProblemAsync(response);
        problem.Title.Should().Be("Conversations.CreatorCannotBeParticipant");
    }

    [Fact]
    public async Task CreateConversation_WithAMissingParticipant_ReturnsNotFoundInsteadOfAnFk500()
    {
        var ahmed = await Factory.CreateUserAsync();

        var before = await QueryDbAsync(database => database.Conversations.CountAsync());

        var response = await ahmed.Client.PostAsJsonAsync("/api/v1/conversations", new
        {
            ParticipantsIds = new[] { Guid.NewGuid() },
            GroupTitle = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var problem = await ReadProblemAsync(response);
        problem.Title.Should().Be("Conversations.ParticipantsNotFound");

        var after = await QueryDbAsync(database => database.Conversations.CountAsync());
        after.Should().Be(before);
    }

    [Fact]
    public async Task CreateGroup_TrimsTheTitleAndAddsTheCreatorAsAParticipant()
    {
        var ahmed = await Factory.CreateUserAsync();
        var sara = await Factory.CreateUserAsync();
        var mohamed = await Factory.CreateUserAsync();

        var conversationId = await CreateConversationAsync(
            ahmed.Client,
            [sara.UserId, mohamed.UserId],
            "  Launch Room  ");

        var detail = await ahmed.Client.GetFromJsonAsync<ConversationDetail>($"/api/v1/conversations/{conversationId}");

        detail!.IsGroup.Should().BeTrue();
        detail.Title.Should().Be("Launch Room");
        detail.Participants.Select(participant => participant.UserId)
            .Should().BeEquivalentTo([ahmed.UserId, sara.UserId, mohamed.UserId]);

        // The creator must be able to read and post to the group they just created.
        var inbox = await ahmed.Client.GetFromJsonAsync<InboxResponse>("/api/v1/conversations");

        inbox!.Conversations.Should().Contain(conversation =>
            conversation.ConversationId == conversationId && conversation.IsGroup);

        using var saraClient = sara.Client;
        using var mohamedClient = mohamed.Client;
    }

    [Fact]
    public async Task CreateGroup_WithoutTitle_ReturnsValidationProblem()
    {
        var ahmed = await Factory.CreateUserAsync();
        var sara = await Factory.CreateUserAsync();
        var mohamed = await Factory.CreateUserAsync();

        var response = await ahmed.Client.PostAsJsonAsync("/api/v1/conversations", new
        {
            ParticipantsIds = new[] { sara.UserId, mohamed.UserId },
            GroupTitle = "   "
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await ReadProblemAsync(response);
        problem.Title.Should().Be("Validation.General");

        var errors = ReadErrors(problem);
        errors.Should().Contain(error => error.Description.Contains("title", StringComparison.OrdinalIgnoreCase));

        using var saraClient = sara.Client;
        using var mohamedClient = mohamed.Client;
    }

    [Fact]
    public async Task CreateGroup_WithTitleOverTheLimit_ReturnsValidationProblem()
    {
        var ahmed = await Factory.CreateUserAsync();
        var sara = await Factory.CreateUserAsync();
        var mohamed = await Factory.CreateUserAsync();

        var response = await ahmed.Client.PostAsJsonAsync("/api/v1/conversations", new
        {
            ParticipantsIds = new[] { sara.UserId, mohamed.UserId },
            GroupTitle = new string('x', ConversationConstants.TitleMaxLength + 1)
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await ReadProblemAsync(response);
        var errors = ReadErrors(problem);
        errors.Should().NotBeEmpty();

        using var saraClient = sara.Client;
        using var mohamedClient = mohamed.Client;
    }

    [Fact]
    public async Task CreateGroup_WithTooManyParticipants_ReturnsValidationProblem()
    {
        var ahmed = await Factory.CreateUserAsync();
        var participantIds = await Factory.SeedUsersAsync(ConversationConstants.MaxParticipants + 1);

        var response = await ahmed.Client.PostAsJsonAsync("/api/v1/conversations", new
        {
            ParticipantsIds = participantIds,
            GroupTitle = "Too big"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await ReadProblemAsync(response);
        var errors = ReadErrors(problem);

        errors.Should().Contain(error =>
            error.Description.Contains(ConversationConstants.MaxParticipants.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task Inbox_IncludesEmptyDirectAndGroupThreadsAndTracksUnreadMessages()
    {
        var ahmed = await Factory.CreateUserAsync();
        var sara = await Factory.CreateUserAsync();
        var mohamed = await Factory.CreateUserAsync();
        var nour = await Factory.CreateUserAsync();

        var directId = await CreateConversationAsync(ahmed.Client, [sara.UserId]);
        var emptyGroupId = await CreateConversationAsync(ahmed.Client, [mohamed.UserId, nour.UserId], "Design Crit");

        var sent = await sara.Client.PostAsJsonAsync(
            $"/api/v1/conversations/{directId}/messages",
            new { Content = "Hello Ahmed" });

        sent.StatusCode.Should().Be(HttpStatusCode.OK);

        var inbox = await ahmed.Client.GetFromJsonAsync<InboxResponse>("/api/v1/conversations");

        inbox.Should().NotBeNull();

        var direct = inbox!.Conversations.Single(conversation => conversation.ConversationId == directId);

        direct.DisplayName.Should().Be(sara.DisplayName);
        direct.IsGroup.Should().BeFalse();
        direct.LastMessageContent.Should().Be("Hello Ahmed");
        direct.LastMessageCreatedAt.Should().NotBeNull();
        direct.UnreadCount.Should().Be(1);
        direct.IsOnline.Should().BeFalse();

        var group = inbox.Conversations.Single(conversation => conversation.ConversationId == emptyGroupId);

        group.DisplayName.Should().Be("Design Crit");
        group.IsGroup.Should().BeTrue();
        group.LastMessageContent.Should().BeNull();
        group.LastMessageCreatedAt.Should().BeNull();
        group.UnreadCount.Should().Be(0);

        using var saraClient = sara.Client;
        using var mohamedClient = mohamed.Client;
        using var nourClient = nour.Client;
    }

    [Fact]
    public async Task Inbox_PaginatesAcrossConversationsWithoutMessages()
    {
        var ahmed = await Factory.CreateUserAsync();
        var sara = await Factory.CreateUserAsync();
        var mohamed = await Factory.CreateUserAsync();
        var nour = await Factory.CreateUserAsync();

        var firstId = await CreateConversationAsync(ahmed.Client, [sara.UserId]);
        var secondId = await CreateConversationAsync(ahmed.Client, [mohamed.UserId]);
        var thirdId = await CreateConversationAsync(ahmed.Client, [nour.UserId]);

        var firstPage = await ahmed.Client.GetFromJsonAsync<InboxResponse>("/api/v1/conversations?pageSize=2");

        firstPage!.Conversations.Should().HaveCount(2);
        firstPage.HasMore.Should().BeTrue();
        firstPage.Nextcursor.Should().NotBeNullOrWhiteSpace();

        var secondPage = await ahmed.Client.GetFromJsonAsync<InboxResponse>(
            $"/api/v1/conversations?pageSize=2&cursor={Uri.EscapeDataString(firstPage.Nextcursor!)}");

        secondPage!.Conversations.Should().HaveCount(1);
        secondPage.HasMore.Should().BeFalse();
        secondPage.Nextcursor.Should().BeNull();

        var ids = firstPage.Conversations.Select(c => c.ConversationId)
            .Concat(secondPage.Conversations.Select(c => c.ConversationId))
            .ToList();

        ids.Should().BeEquivalentTo([firstId, secondId, thirdId]);

        using var saraClient = sara.Client;
        using var mohamedClient = mohamed.Client;
        using var nourClient = nour.Client;
    }

    [Fact]
    public async Task ConversationDetail_NonParticipantGets403_UnknownGets404()
    {
        var ahmed = await Factory.CreateUserAsync();
        var sara = await Factory.CreateUserAsync();
        var outsider = await Factory.CreateUserAsync();

        var conversationId = await CreateConversationAsync(ahmed.Client, [sara.UserId]);

        var forbidden = await outsider.Client.GetAsync($"/api/v1/conversations/{conversationId}");

        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var forbiddenProblem = await ReadProblemAsync(forbidden);
        forbiddenProblem.Title.Should().Be("Conversations.AccessDenied");

        var missing = await ahmed.Client.GetAsync($"/api/v1/conversations/{Guid.NewGuid()}");

        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var missingProblem = await ReadProblemAsync(missing);
        missingProblem.Title.Should().Be("Conversations.NotFound");

        using var saraClient = sara.Client;
        using var outsiderClient = outsider.Client;
    }

    [Fact]
    public async Task MarkAsRead_ClearsUnreadCountAndUpdatesLastSeen()
    {
        var ahmed = await Factory.CreateUserAsync();
        var sara = await Factory.CreateUserAsync();

        var conversationId = await CreateConversationAsync(ahmed.Client, [sara.UserId]);

        await sara.Client.PostAsJsonAsync($"/api/v1/conversations/{conversationId}/messages", new { Content = "One" });
        var last = await sara.Client.PostAsJsonAsync($"/api/v1/conversations/{conversationId}/messages", new { Content = "Two" });
        var lastId = await last.Content.ReadFromJsonAsync<Guid>();

        var before = await ahmed.Client.GetFromJsonAsync<InboxResponse>("/api/v1/conversations");
        before!.Conversations.Single(c => c.ConversationId == conversationId).UnreadCount.Should().Be(2);

        var markRead = await ahmed.Client.PostAsync($"/api/v1/conversations/{conversationId}/mark-as-read", null);
        markRead.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await ahmed.Client.GetFromJsonAsync<InboxResponse>("/api/v1/conversations");
        after!.Conversations.Single(c => c.ConversationId == conversationId).UnreadCount.Should().Be(0);

        var messages = await ahmed.Client.GetFromJsonAsync<MessagesResponse>(
            $"/api/v1/conversations/{conversationId}/messages");

        messages!.LastSeenMessageId.Should().Be(lastId);

        using var saraClient = sara.Client;
    }

    [Fact]
    public async Task LeaveGroup_RemovesMembershipWhileLeavingADirectConversationIsANoOp()
    {
        var ahmed = await Factory.CreateUserAsync();
        var sara = await Factory.CreateUserAsync();
        var mohamed = await Factory.CreateUserAsync();

        var groupId = await CreateConversationAsync(ahmed.Client, [sara.UserId, mohamed.UserId], "Backend Team");

        var leave = await sara.Client.DeleteAsync($"/api/v1/conversations/{groupId}/participants/me");
        leave.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterLeave = await sara.Client.GetAsync($"/api/v1/conversations/{groupId}");
        afterLeave.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var leaveAgain = await sara.Client.DeleteAsync($"/api/v1/conversations/{groupId}/participants/me");
        leaveAgain.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var creatorView = await ahmed.Client.GetFromJsonAsync<ConversationDetail>($"/api/v1/conversations/{groupId}");
        creatorView!.Participants.Should().HaveCount(2);

        var directId = await CreateConversationAsync(ahmed.Client, [mohamed.UserId]);

        var leaveDirect = await ahmed.Client.DeleteAsync($"/api/v1/conversations/{directId}/participants/me");
        leaveDirect.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var directView = await ahmed.Client.GetAsync($"/api/v1/conversations/{directId}");
        directView.StatusCode.Should().Be(HttpStatusCode.OK);

        using var saraClient = sara.Client;
        using var mohamedClient = mohamed.Client;
    }

    private static async Task<Guid> CreateConversationAsync(
        HttpClient client,
        IReadOnlyList<Guid> participantIds,
        string? title = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/conversations", new
        {
            ParticipantsIds = participantIds,
            GroupTitle = title
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

    private sealed record ConversationDetail(
        Guid ConversationId,
        string? Title,
        bool IsGroup,
        IReadOnlyList<ConversationParticipant> Participants);

    private sealed record ConversationParticipant(
        Guid UserId,
        string DisplayName,
        string? ProfilePictureUrl,
        bool IsOnline,
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
