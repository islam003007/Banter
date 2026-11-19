using Banter.Infrastructure.Database;

namespace Banter.IntegrationTests.DataSeed;

public class SeedSmokeTests : IntegrationTestBase
{
    public SeedSmokeTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Migrations_AreApplied()
    {
        var migrations = await QueryDbAsync(database =>
            database.Database.SqlQueryRaw<string>(
                    "SELECT \"MigrationId\" AS \"Value\" FROM \"__EFMigrationsHistory\"")
                .ToListAsync());

        migrations.Should().Contain("20260601201718_InitialCreate");
        migrations.Should().Contain("20261118044042_AddBotUsers");
    }

    [Fact]
    public async Task DevelopmentSeed_CreatesDemoUsersBotsAndConversations()
    {
        var users = await QueryDbAsync(database => database.Users.CountAsync(user => !user.IsBot));
        var bots = await QueryDbAsync(database => database.Users.CountAsync(user => user.IsBot));
        var conversations = await QueryDbAsync(database => database.Conversations.CountAsync());

        users.Should().Be(6);
        bots.Should().Be(3);
        conversations.Should().BeGreaterThanOrEqualTo(6);

        var ada = await QueryDbAsync(database =>
            database.Users.SingleAsync(user => user.Email == "ada@bots.local"));

        ada.IsBot.Should().BeTrue();
        ada.PasswordHash.Should().BeNull("bot accounts are seeded without a password and can never sign in");
    }

    [Fact]
    public async Task ResetDatabase_KeepsMigrationHistoryAndReappliesTheSeed()
    {
        // The base class already reset + reseeded once for this test. Run it a second time to
        // prove ResetDatabaseAsync is genuinely idempotent and keeps the migration table.
        await Factory.ResetDatabaseAsync();

        var migrationCount = await QueryDbAsync(database =>
            database.Database.SqlQueryRaw<int>(
                    "SELECT COUNT(*)::int AS \"Value\" FROM \"__EFMigrationsHistory\"")
                .SingleAsync());

        var userCount = await QueryDbAsync(database => database.Users.CountAsync(user => !user.IsBot));

        migrationCount.Should().BeGreaterThanOrEqualTo(2);
        userCount.Should().Be(6);
    }
}
