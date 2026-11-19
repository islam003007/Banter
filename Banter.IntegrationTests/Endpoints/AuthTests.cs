using Microsoft.AspNetCore.Mvc;

namespace Banter.IntegrationTests.Endpoints;

public class AuthTests : IntegrationTestBase
{
    public AuthTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Register_ReturnsTheUserIdAndSignsTheUserIn()
    {
        using var client = Factory.CreateClient();
        var email = $"register.{Guid.NewGuid():N}@test.com";

        var response = await RegisterAsync(client, email, displayName: "Register User");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var userId = await response.Content.ReadFromJsonAsync<Guid>();
        userId.Should().NotBeEmpty();

        var me = await client.GetFromJsonAsync<CurrentUser>("/api/v1/auth/me");

        me.Should().NotBeNull();
        me!.Id.Should().Be(userId);
        me.Email.Should().Be(email);
        me.DisplayName.Should().Be("Register User");
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsAValidationProblem()
    {
        using var client = Factory.CreateClient();
        var email = $"duplicate.{Guid.NewGuid():N}@test.com";

        (await RegisterAsync(client, email)).EnsureSuccessStatusCode();

        var response = await RegisterAsync(client, email);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await ReadProblemAsync(response);
        var errors = ReadErrors(problem);

        errors.Should().NotBeEmpty();
        errors.Select(error => error.Code).Should().Contain(code => code.Contains("Duplicate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Register_PasswordConfirmationMismatch_ReturnsFluentValidationErrors()
    {
        using var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/users/register", new
        {
            Email = $"mismatch.{Guid.NewGuid():N}@test.com",
            DisplayName = "Mismatch",
            Password = "Password@123",
            PasswordConfirm = "Password@124"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await ReadProblemAsync(response);
        problem.Title.Should().Be("Validation.General");

        var errors = ReadErrors(problem);
        errors.Should().NotBeEmpty();
        errors.Should().Contain(error => error.Description.Contains("PasswordConfirm", StringComparison.OrdinalIgnoreCase)
            || error.Description.Contains("password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOkAndAnAuthCookie()
    {
        var user = await Factory.CreateUserAsync();
        using var client = Factory.CreateClient();

        var response = await LoginAsync(client, user.Email, user.Password);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        CookieHeader(response).Should().StartWith("Banter.Auth=");

        var setCookie = response.Headers.GetValues("Set-Cookie").Single();
        setCookie.ToLowerInvariant().Should().Contain("httponly");
        setCookie.ToLowerInvariant().Should().Contain("samesite=lax");

        var me = await client.GetFromJsonAsync<CurrentUser>("/api/v1/auth/me");

        me!.Email.Should().Be(user.Email);
    }

    [Fact]
    public async Task Login_WithTheWrongPassword_ReturnsTheGenericLoginFailure()
    {
        var user = await Factory.CreateUserAsync();
        using var client = Factory.CreateClient();

        var response = await LoginAsync(client, user.Email, "Wrong@123");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await ReadProblemAsync(response);
        problem.Title.Should().Be("Auth.loginFailed");
    }

    [Fact]
    public async Task Login_AsASeededBot_IsAlwaysRejected()
    {
        using var client = Factory.CreateClient();

        var response = await LoginAsync(client, "ada@bots.local", "Password@123");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await ReadProblemAsync(response);
        problem.Title.Should().Be("Auth.loginFailed");
        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeFalse();
    }

    [Fact]
    public async Task Logout_ClearsTheCookieSoMeReturnsUnauthorized()
    {
        var user = await Factory.CreateUserAsync();

        var logout = await user.Client.PostAsync("/api/v1/auth/logout", content: null);

        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var me = await user.Client.GetAsync("/api/v1/auth/me");

        me.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_WithoutAuthentication_ReturnsTheProblemDetailsShape()
    {
        var response = await Client.GetAsync("/api/v1/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var problem = await ReadProblemAsync(response);

        problem.Status.Should().Be(401);
        problem.Title.Should().Be("Unauthorized");
        problem.Detail.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task DemoUsers_ReturnsTheSeededAllowListOnly()
    {
        var users = await Client.GetFromJsonAsync<List<DemoUser>>("/api/v1/auth/demo-users");

        users.Should().NotBeNull();
        users!.Should().HaveCount(6);
        users.Select(user => user.Email).Should().ContainInOrder(
            "ahmed@example.com",
            "sara@example.com",
            "mohamed@example.com",
            "nour@example.com",
            "omar@example.com",
            "fatma@example.com");
        users.Should().NotContain(user => user.Email.EndsWith("@bots.local", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DemoLogin_AllowedSeededUser_SignsInWithoutAPassword()
    {
        var users = await Client.GetFromJsonAsync<List<DemoUser>>("/api/v1/auth/demo-users");
        var sara = users!.Single(user => user.Email == "sara@example.com");

        using var client = Factory.CreateClient();

        var response = await DemoLoginAsync(client, sara.Id);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var me = await client.GetFromJsonAsync<CurrentUser>("/api/v1/auth/me");

        me!.Id.Should().Be(sara.Id);
        me.Email.Should().Be(sara.Email);
    }

    [Fact]
    public async Task DemoLogin_BotOrUnknownUser_IsRejected()
    {
        var botId = await QueryDbAsync(database =>
            database.Users.Where(user => user.Email == "ada@bots.local").Select(user => user.Id).SingleAsync());

        using var client = Factory.CreateClient();

        var botResponse = await DemoLoginAsync(client, botId);
        botResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var unknownResponse = await DemoLoginAsync(client, Guid.NewGuid());
        unknownResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var problem = await ReadProblemAsync(unknownResponse);
        problem.Title.Should().Be("Demo.UserNotAllowed");
    }

    private sealed record CurrentUser(Guid Id, string Email, string DisplayName, string? ProfilePictureUrl);

    private sealed record DemoUser(Guid Id, string Email, string DisplayName, string? ProfilePictureUrl);
}
