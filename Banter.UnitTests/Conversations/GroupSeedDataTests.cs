using Banter.Application.Features.Conversations;
using Banter.Domain.Conversations;
using Banter.Domain.Messages;
using Banter.Domain.Users;
using Banter.Infrastructure.Database;
using Banter.Infrastructure.Database.DataSeed;
using Banter.Infrastructure.Database.Seed;
using Banter.UnitTests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Banter.UnitTests.Conversations;

public class GroupSeedDataTests
{
    private static readonly Guid _ahmedId = Guid.Parse("019e8b9d-c84a-75a0-b965-7d25b890ad80");
    private static readonly Guid _weekendTripId = Guid.Parse("019e8b9f-a5f5-754a-ae80-d54e9633b727");
    private static readonly Guid _launchRoomId = Guid.Parse("019e8bc6-1000-7000-8000-000000000001");
    private static readonly Guid _designCritId = Guid.Parse("019e8bc6-1000-7000-8000-000000000002");

    [Fact]
    public void GroupSeedData_AddsDistinctHumanGroupsWithoutReplacingOriginals()
    {
        var originals = JsonDataLoader.LoadAsync<Conversation>("Conversations.json");
        var groups = JsonDataLoader.LoadAsync<Conversation>("GroupConversations.json");

        Assert.Equal(4, originals.Count);
        Assert.Equal(2, groups.Count);
        Assert.All(groups, group => Assert.True(group.IsGroup));
        Assert.All(groups, group => Assert.False(string.IsNullOrWhiteSpace(group.Title)));
        Assert.Equal(
            ["Design Crit", "Launch Room"],
            groups.Select(group => group.Title).OrderBy(title => title).ToList());

        var allIds = originals.Concat(groups).Select(conversation => conversation.Id).ToList();

        Assert.Equal(allIds.Count, allIds.Distinct().Count());
        Assert.Contains(originals, conversation => conversation.Title == "Backend Team");
        Assert.Contains(originals, conversation => conversation.Title == "Weekend Trip");
    }

    [Fact]
    public void Ahmed_IsAParticipantInEverySeededGroup()
    {
        var conversations = JsonDataLoader.LoadAsync<Conversation>("Conversations.json")
            .Concat(JsonDataLoader.LoadAsync<Conversation>("GroupConversations.json"))
            .Where(conversation => conversation.IsGroup)
            .ToList();

        var participants = JsonDataLoader.LoadAsync<ConversationParticipant>("ConversationParticipants.json")
            .Concat(JsonDataLoader.LoadAsync<ConversationParticipant>("GroupParticipants.json"))
            .ToList();

        Assert.Equal(4, conversations.Count);
        Assert.All(conversations, conversation =>
            Assert.Contains(
                participants,
                participant => participant.ConversationId == conversation.Id && participant.UserId == _ahmedId));

        // Existing databases already have Weekend Trip, so Ahmed's membership must come from the
        // idempotent GroupDataSeeder file rather than the one-shot base seed.
        var baseParticipants = JsonDataLoader.LoadAsync<ConversationParticipant>("ConversationParticipants.json");
        var groupParticipants = JsonDataLoader.LoadAsync<ConversationParticipant>("GroupParticipants.json");

        Assert.DoesNotContain(
            baseParticipants,
            participant => participant.ConversationId == _weekendTripId && participant.UserId == _ahmedId);
        Assert.Contains(
            groupParticipants,
            participant => participant.ConversationId == _weekendTripId && participant.UserId == _ahmedId);
    }

    [Fact]
    public void LaunchRoom_HasMultiPersonFlowAndUnreadMessagesForAhmed()
    {
        var participants = JsonDataLoader.LoadAsync<ConversationParticipant>("GroupParticipants.json")
            .Where(participant => participant.ConversationId == _launchRoomId)
            .ToList();

        var messages = JsonDataLoader.LoadAsync<Message>("GroupMessages.json")
            .Where(message => message.ConversationId == _launchRoomId)
            .OrderBy(message => message.CreatedAt)
            .ThenBy(message => message.Id)
            .ToList();

        Assert.Equal(4, participants.Count);
        Assert.Equal(4, participants.Select(participant => participant.UserId).Distinct().Count());
        Assert.Equal(7, messages.Count);
        Assert.True(messages.Select(message => message.UserId).Distinct().Count() >= 3);

        var ahmedParticipant = participants.Single(participant => participant.UserId == _ahmedId);
        var lastSeenMessageId = Assert.IsType<Guid>(ahmedParticipant.LastSeenMessageId);
        var lastSeen = messages.Single(message => message.Id == lastSeenMessageId);

        var unread = messages
            .Where(message => message.UserId != _ahmedId
                && (message.CreatedAt > lastSeen.CreatedAt
                    || (message.CreatedAt == lastSeen.CreatedAt && message.Id > lastSeen.Id)))
            .ToList();

        Assert.Equal(4, unread.Count);
        Assert.True(unread.Select(message => message.UserId).Distinct().Count() >= 2);
        Assert.All(messages, message =>
            Assert.Contains(message.UserId, participants.Select(participant => participant.UserId)));
    }

    [Fact]
    public async Task GroupDataSeeder_IsIdempotentAndResolvesLastMessageIds()
    {
        await using var provider = CreateProvider();

        await SeedOriginalDataAsync(provider);

        await GroupDataSeeder.SeedAsync(provider);
        await GroupDataSeeder.SeedAsync(provider);

        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Equal(6, await dbContext.Conversations.CountAsync());
        Assert.Equal(32, await dbContext.Messages.CountAsync());
        Assert.Equal(21, await dbContext.ConversationParticipants.CountAsync());

        var conversations = await dbContext.Conversations.ToListAsync();
        var groupConversations = conversations.Where(conversation => conversation.IsGroup).ToList();

        Assert.Equal(4, groupConversations.Count);

        foreach (var conversation in groupConversations)
        {
            Assert.NotNull(conversation.LastMessageId);

            var latestMessageId = await dbContext.Messages
                .Where(message => message.ConversationId == conversation.Id)
                .OrderByDescending(message => message.CreatedAt)
                .ThenByDescending(message => message.Id)
                .Select(message => message.Id)
                .FirstAsync();

            Assert.Equal(latestMessageId, conversation.LastMessageId);
        }
    }

    [Fact]
    public async Task AhmedInbox_ShowsTheNewGroupsAndTheirUnreadState()
    {
        await using var provider = CreateProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            dbContext.Users.AddRange(JsonDataLoader.LoadAsync<User>("Users.json"));
            await dbContext.SaveChangesAsync();
        }

        await SeedOriginalDataAsync(provider);

        await GroupDataSeeder.SeedAsync(provider);

        await using var readScope = provider.CreateAsyncScope();
        var readDbContext = readScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var handler = new GetInboxHandler(
            readDbContext,
            new FakeUserContext(_ahmedId),
            new FakePresenceService());

        var result = await handler.Handle(new GetInboxQuery(null, 20), CancellationToken.None);

        Assert.True(result.IsSuccess);

        var conversations = result.Value.Conversations;

        Assert.Equal(6, conversations.Count);

        var launchRoom = conversations.Single(conversation => conversation.ConversationId == _launchRoomId);
        Assert.Equal("Launch Room", launchRoom.DisplayName);
        Assert.True(launchRoom.IsGroup);
        Assert.Equal(4, launchRoom.UnreadCount);
        Assert.Equal(
            "Pushed a fix with a regression test. Ready for another pass.",
            launchRoom.LastMessageContent);

        var weekendTrip = conversations.Single(conversation => conversation.ConversationId == _weekendTripId);
        Assert.Equal("Weekend Trip", weekendTrip.DisplayName);
        Assert.True(weekendTrip.IsGroup);
        Assert.Equal(0, weekendTrip.UnreadCount);

        var designCrit = conversations.Single(conversation => conversation.ConversationId == _designCritId);
        Assert.Equal("Design Crit", designCrit.DisplayName);
        Assert.True(designCrit.IsGroup);
        Assert.Equal(0, designCrit.UnreadCount);
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        var databaseName = $"GroupSeed-{Guid.NewGuid()}";

        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));

        return services.BuildServiceProvider();
    }

    private static async Task SeedOriginalDataAsync(IServiceProvider provider)
    {
        await DataSeeder.SeedAsync<Conversation>(provider, "Conversations.json");
        await DataSeeder.SeedAsync<Message>(provider, "Messages.json");
        await DataSeeder.SeedAsync<ConversationParticipant>(provider, "ConversationParticipants.json");
    }
}
