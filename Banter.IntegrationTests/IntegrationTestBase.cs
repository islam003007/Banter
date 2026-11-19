using System.Text.Json;
using Banter.Infrastructure.Database;
using Microsoft.AspNetCore.Mvc;

namespace Banter.IntegrationTests;

[Collection("IntegrationTests")]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected IntegrationTestWebAppFactory Factory { get; }

    protected HttpClient Client { get; private set; } = null!;

    protected IntegrationTestBase(IntegrationTestWebAppFactory factory)
    {
        Factory = factory;
    }

    public virtual async ValueTask InitializeAsync()
    {
        if (Factory.SkipReason is not null)
            Assert.Skip(Factory.SkipReason);

        Client = Factory.CreateClient();

        await Factory.ResetDatabaseAsync();
    }

    public virtual ValueTask DisposeAsync()
    {
        Client?.Dispose();

        return ValueTask.CompletedTask;
    }

    protected async Task<HttpResponseMessage> RegisterAsync(
        HttpClient client,
        string email,
        string password = "Password@123",
        string displayName = "Test User")
    {
        return await client.PostAsJsonAsync("/api/v1/users/register", new
        {
            Email = email,
            DisplayName = displayName,
            ProfilePictureUrl = (string?)null,
            Password = password,
            PasswordConfirm = password
        });
    }

    protected static async Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string email,
        string password = "Password@123")
    {
        return await client.PostAsJsonAsync("/api/v1/auth/login", new { Email = email, Password = password });
    }

    protected static async Task<HttpResponseMessage> DemoLoginAsync(HttpClient client, Guid userId)
    {
        return await client.PostAsJsonAsync("/api/v1/auth/demo-login", new { UserId = userId });
    }

    protected static async Task<Guid> ReadGuidAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    protected static async Task<ProblemDetails> ReadProblemAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        problem.Should().NotBeNull();

        return problem!;
    }

    protected static IReadOnlyList<ApiError> ReadErrors(ProblemDetails problem)
    {
        if (!problem.Extensions.TryGetValue("errors", out var value) || value is not JsonElement errors)
            return [];

        return errors.EnumerateArray()
            .Select(error => new ApiError(
                error.GetProperty("code").GetString() ?? string.Empty,
                error.GetProperty("description").GetString() ?? string.Empty))
            .ToList();
    }

    protected async Task<T> QueryDbAsync<T>(Func<AppDbContext, Task<T>> query)
    {
        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await query(database);
    }

    protected static async Task WaitUntilAsync(
        Func<Task<bool>> condition,
        TimeSpan? timeout = null,
        string? because = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));

        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
                return;

            await Task.Delay(50);
        }

        Assert.Fail(because ?? "The expected condition was not observed before the timeout.");
    }

    protected static string CookieHeader(HttpResponseMessage response)
    {
        var setCookie = response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault()
            : null;

        setCookie.Should().NotBeNullOrWhiteSpace("a successful login or registration must set the auth cookie");

        return setCookie!.Split(';', 2)[0];
    }

    protected sealed record ApiError(string Code, string Description);
}
