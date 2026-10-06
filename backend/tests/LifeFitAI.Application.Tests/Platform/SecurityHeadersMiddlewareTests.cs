using FluentAssertions;
using LifeFitAI.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace LifeFitAI.Application.Tests.Platform;

public sealed class SecurityHeadersMiddlewareTests
{
    [Fact]
    public async Task ApiResponses_UseClosedContentSecurityPolicy()
    {
        var context = await InvokeAsync("Development", "/api/v1/auth/login");

        context.Response.Headers.ContentSecurityPolicy.ToString()
            .Should().Be("default-src 'none'; frame-ancestors 'none'");
    }

    [Fact]
    public async Task DevelopmentSwagger_AllowsUiAssets()
    {
        var context = await InvokeAsync("Development", "/swagger/index.html");
        var policy = context.Response.Headers.ContentSecurityPolicy.ToString();

        policy.Should().Contain("script-src 'self'");
        policy.Should().Contain("style-src 'self' 'unsafe-inline'");
        policy.Should().Contain("connect-src 'self'");
    }

    [Fact]
    public async Task ProductionSwagger_StaysClosed()
    {
        var context = await InvokeAsync("Production", "/swagger/index.html");

        context.Response.Headers.ContentSecurityPolicy.ToString()
            .Should().Be("default-src 'none'; frame-ancestors 'none'");
        context.Response.Headers.StrictTransportSecurity.ToString()
            .Should().Be("max-age=31536000; includeSubDomains");
    }

    private static async Task<DefaultHttpContext> InvokeAsync(string environment, string path)
    {
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask, new TestHostEnvironment(environment));
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        await middleware.InvokeAsync(context);
        return context;
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string environmentName) => EnvironmentName = environmentName;

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "LifeFitAI.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
