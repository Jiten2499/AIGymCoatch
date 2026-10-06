using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LifeFitAI.SharedKernel.Constants;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LifeFitAI.Application.Tests.Health;

[Collection("LifeFitApi")]
public sealed class HealthEndpointTests : IClassFixture<LifeFitApiFactory>
{
    private readonly LifeFitApiFactory _factory;

    public HealthEndpointTests(LifeFitApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetLive_ReturnsHealthy()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/live");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Be("Healthy");
        response.Headers.Contains(HeaderNames.CorrelationId).Should().BeTrue();
    }

    [Fact]
    public async Task ControllerRoutes_KeepPlatformAndProtectedFitness()
    {
        using var client = _factory.CreateClient();

        var platform = await client.GetAsync("/api/v1/platform");
        var profile = await client.GetAsync("/api/v1/fitness/profile");
        var coach = await client.PostAsJsonAsync("/api/v1/ai/coach/chat", new { message = "hello" });

        platform.StatusCode.Should().Be(HttpStatusCode.OK);
        profile.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        coach.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

[CollectionDefinition("LifeFitApi", DisableParallelization = true)]
public sealed class LifeFitApiCollection;

public sealed class LifeFitApiFactory : WebApplicationFactory<Program>
{
    static LifeFitApiFactory()
    {
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__SqlServer",
            "Server=127.0.0.1,1433;Database=LifeFitAI;User Id=sa;Password=placeholder;TrustServerCertificate=True;Encrypt=False");
        Environment.SetEnvironmentVariable(
            "Jwt__SigningKey",
            "test-signing-key-at-least-32-bytes-long!!");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }
}
