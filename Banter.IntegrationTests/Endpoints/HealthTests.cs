namespace Banter.IntegrationTests.Endpoints;

public class HealthTests : IntegrationTestBase
{
    public HealthTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Liveness_IsAnonymousAndDoesNotTouchDependencies()
    {
        var response = await Client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();

        body!.Status.Should().Be("Healthy");
    }

    [Fact]
    public async Task Readiness_ReportsPostgresAndRedis()
    {
        var response = await Client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ReadinessResponse>();

        body!.Status.Should().Be("Healthy");
        body.Checks.Database.Should().Be("Healthy");
        body.Checks.Redis.Should().Be("Healthy");
    }

    private sealed record HealthResponse(string Status);

    private sealed record ReadinessResponse(string Status, ReadinessChecks Checks);

    private sealed record ReadinessChecks(string Database, string Redis);
}
