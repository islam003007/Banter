using Banter.Application.Abstractions.Ai;
using Banter.Application.Bots;
using Banter.Infrastructure.Services.Ai;
using Xunit;

namespace Banter.UnitTests.Bots;

public class DemoAiChatClientTests
{
    private readonly DemoAiChatClient _client = new();

    [Fact]
    public async Task GenerateReplyAsync_IsDeterministicForTheSameInput()
    {
        var request = CreateRequest(BotPersonas.Ada, "How should I make the worker reliable?");

        var first = await _client.GenerateReplyAsync(request);
        var second = await _client.GenerateReplyAsync(request);

        Assert.Equal(first, second);
        Assert.False(string.IsNullOrWhiteSpace(first));
        Assert.DoesNotContain("{name}", first);
    }

    [Fact]
    public async Task GenerateReplyAsync_GivesEachPersonaADifferentVoice()
    {
        const string content = "The bot stopped replying after I moved the worker.";

        var ada = await _client.GenerateReplyAsync(CreateRequest(BotPersonas.Ada, content));
        var milo = await _client.GenerateReplyAsync(CreateRequest(BotPersonas.Milo, content));
        var nova = await _client.GenerateReplyAsync(CreateRequest(BotPersonas.Nova, content));

        Assert.NotEqual(ada, milo);
        Assert.NotEqual(ada, nova);
        Assert.NotEqual(milo, nova);
    }

    [Fact]
    public async Task GenerateReplyAsync_AnswersGreetingsByPersona()
    {
        var request = CreateRequest(BotPersonas.Milo, "Hello!");

        var reply = await _client.GenerateReplyAsync(request);

        Assert.Contains("Ahmed", reply);
        Assert.False(string.IsNullOrWhiteSpace(reply));
    }

    [Fact]
    public async Task GenerateReplyAsync_FallsBackWhenThereIsNoHistory()
    {
        var request = new BotChatRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            BotPersonas.Nova.Email,
            BotPersonas.Nova.DisplayName,
            Guid.NewGuid(),
            "Ahmed",
            []);

        var reply = await _client.GenerateReplyAsync(request);

        Assert.Contains("Nova", reply);
    }

    private static BotChatRequest CreateRequest(BotPersona persona, string content) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            persona.Email,
            persona.DisplayName,
            Guid.NewGuid(),
            "Ahmed",
            [
                new BotChatMessage(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "Ahmed",
                    IsBot: false,
                    content,
                    DateTime.UtcNow)
            ]);
}
