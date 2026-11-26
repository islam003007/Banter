using Banter.Application.Abstractions;
using Banter.Domain.Users;
using Banter.Infrastructure.Database;
using Banter.Infrastructure.Database.DataSeed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Respawn;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

// The suite shares one Docker-backed factory and Respawn resets the database per test, so the
// assembly must run sequentially (same strategy as FalkElTawfik.IntegrationTests).
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Banter.IntegrationTests;

public sealed class IntegrationTestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:14.22-trixie")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:8.6.2-trixie")
        .Build();

    private Respawner _respawner = null!;
    private bool _postgresStarted;
    private bool _redisStarted;

    /// <summary>
    /// Set when Testcontainers/Docker cannot start. Every test calls <c>Assert.Skip</c> with this
    /// reason in <see cref="IntegrationTestBase.InitializeAsync"/> rather than failing with a
    /// misleading connection error.
    /// </summary>
    public string? SkipReason { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
    }

    public async ValueTask InitializeAsync()
    {
        try
        {
            await _postgres.StartAsync();
            _postgresStarted = true;

            await _redis.StartAsync();
            _redisStarted = true;
        }
        catch (Exception exception)
        {
            SkipReason = $"Docker/Testcontainers is unavailable: {exception.Message}";

            return;
        }

        // Program reads these during WebApplication.CreateBuilder. Environment variables are set
        // before the first Services/CreateClient call, which is when the host is actually built.
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", _redis.GetConnectionString());
        Environment.SetEnvironmentVariable("DataSeed__UsersPassword", "Password@123");
        Environment.SetEnvironmentVariable("Demo__Enabled", "true");
        Environment.SetEnvironmentVariable("Bots__Enabled", "true");
        Environment.SetEnvironmentVariable("RateLimit__Enabled", "false");
        // appsettings.json points Seq at the compose hostname; point the sink at an unreachable
        // local port instead. The sink batches asynchronously and never blocks requests.
        Environment.SetEnvironmentVariable("Serilog__WriteTo__1__Args__serverUrl", "http://localhost:9999");

        // Touch the host: Program applies migrations and seeds development data at startup.
        using (var scope = Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await database.Database.MigrateAsync();
        }

        await using (var connection = new NpgsqlConnection(_postgres.GetConnectionString()))
        {
            await connection.OpenAsync();

            _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
                SchemasToInclude = ["public", "identity"],
                TablesToIgnore = ["__EFMigrationsHistory"]
            });
        }
    }

    /// <summary>
    /// Respawns the database between tests, keeps the migration history, then re-runs the
    /// development seeders so every test starts from the same demo fixture.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        if (SkipReason is not null)
            return;

        await using (var connection = new NpgsqlConnection(_postgres.GetConnectionString()))
        {
            await connection.OpenAsync();
            await _respawner.ResetAsync(connection);
        }

        await SeederRunner.SeedDevelopment(Services);

        // Presence (and the SignalR backplane/data-protection keys) live in Redis, outside
        // Respawn's reach. Flushing the tiny test database gives every test a clean presence view;
        // the host recreates any keys it still needs.
        var redis = Services.GetRequiredService<IConnectionMultiplexer>();
        await redis.GetDatabase().ExecuteAsync("FLUSHDB");
    }

    public async Task<TestUser> CreateUserAsync(string? email = null, string password = "Password@123")
    {
        var client = CreateClient();
        email ??= $"user.{Guid.NewGuid():N}@test.com";
        var displayName = $"Test {email.Split('@')[0]}";

        var response = await client.PostAsJsonAsync("/api/v1/users/register", new
        {
            Email = email,
            DisplayName = displayName,
            ProfilePictureUrl = (string?)null,
            Password = password,
            PasswordConfirm = password
        });

        response.EnsureSuccessStatusCode();

        var userId = await response.Content.ReadFromJsonAsync<Guid>();

        return new TestUser(client, userId, email, password, displayName);
    }

    /// <summary>Seeds extra identity users without going through the password hasher (fast).</summary>
    public async Task<IReadOnlyList<Guid>> SeedUsersAsync(int count)
    {
        using var scope = Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var users = Enumerable.Range(0, count)
            .Select(index => new User(
                $"seed.{Guid.NewGuid():N}@test.com",
                $"Seed User {index}",
                profilePictureUrl: null))
            .ToList();

        database.Users.AddRange(users);
        await database.SaveChangesAsync();

        return users.Select(user => user.Id).ToList();
    }

    public Task<int> GetConnectionCountAsync(Guid userId)
        => Services.GetRequiredService<IPresenceService>()
            .GetConnectionCountAsync(userId, CancellationToken.None);

    public new async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        if (_redisStarted)
            await _redis.StopAsync();

        if (_postgresStarted)
            await _postgres.StopAsync();
    }
}

public sealed record TestUser(
    HttpClient Client,
    Guid UserId,
    string Email,
    string Password,
    string DisplayName);

[CollectionDefinition("IntegrationTests")]
public class IntegrationTestCollection : ICollectionFixture<IntegrationTestWebAppFactory>
{
}
