namespace Banter.IntegrationTests.Endpoints;

public class SearchTests : IntegrationTestBase
{
    public SearchTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Search_IsCaseInsensitiveForDisplayNameAndEmail()
    {
        var searcher = await Factory.CreateUserAsync();

        // Register a user with a clear display name so the assertion is not coupled to the
        // CreateUserAsync helper's generated naming scheme.
        using var adaClient = Factory.CreateClient();
        var response = await RegisterAsync(
            adaClient,
            $"ada.lovelace.{Guid.NewGuid():N}@example.com",
            displayName: "Ada Lovelace");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var byDisplayName = await searcher.Client.GetFromJsonAsync<SearchResponse>(
            "/api/v1/users/search?searchTerm=ADA%20LOVELACE");

        byDisplayName!.Users.Should().Contain(user =>
            user.DisplayName == "Ada Lovelace" && user.Email.Contains("ada.lovelace", StringComparison.OrdinalIgnoreCase));

        var byEmail = await searcher.Client.GetFromJsonAsync<SearchResponse>(
            "/api/v1/users/search?searchTerm=ADA.LOVELACE");

        byEmail!.Users.Should().Contain(user =>
            user.DisplayName == "Ada Lovelace" && user.Email.Contains("ada.lovelace", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Search_ReturnsTheBotFlag()
    {
        var searcher = await Factory.CreateUserAsync();

        var response = await searcher.Client.GetFromJsonAsync<SearchResponse>(
            "/api/v1/users/search?searchTerm=ada%40bots.local");

        response!.Users.Should().ContainSingle(user => user.Email == "ada@bots.local");
        response.Users.Single(user => user.Email == "ada@bots.local").IsBot.Should().BeTrue();
    }

    [Fact]
    public async Task Search_RequiresAuthentication()
    {
        var response = await Client.GetAsync("/api/v1/users/search?searchTerm=ada");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Search_WithAShortOrWhitespaceTerm_ReturnsValidationProblem()
    {
        var searcher = await Factory.CreateUserAsync();

        var shortTerm = await searcher.Client.GetAsync("/api/v1/users/search?searchTerm=a");
        shortTerm.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var whitespace = await searcher.Client.GetAsync("/api/v1/users/search?searchTerm=%20%20%20");
        whitespace.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await ReadProblemAsync(whitespace);
        problem.Title.Should().Be("Validation.General");
    }

    private sealed record SearchResponse(
        IReadOnlyList<SearchUser> Users,
        string? NextCursor,
        bool HasMore);

    private sealed record SearchUser(
        Guid Id,
        string DisplayName,
        string Email,
        string? ProfilePicture,
        bool IsOnline,
        bool IsBot);
}
