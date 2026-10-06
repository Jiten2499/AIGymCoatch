using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;

namespace LifeFitAI.Api.Extensions;

public static class ApplicationBuilderExtensions
{
    public static WebApplication UseLifeFitPlatform(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseMiddleware<Middleware.SecurityHeadersMiddleware>();
        app.UseMiddleware<Middleware.CorrelationIdMiddleware>();
        app.UseMiddleware<Middleware.MaintenanceMiddleware>();
        app.UseSerilogRequestLogging();
        app.UseMiddleware<Middleware.GlobalExceptionMiddleware>();

        if (app.Environment.IsDevelopment())
        {
            app.UseCors(ServiceCollectionExtensions.LocalClientsCorsPolicy);
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "V1");
            });
        }
        else
        {
            app.UseCors(ServiceCollectionExtensions.ConfiguredCorsPolicy);
        }

        if (app.Environment.IsProduction()
            || !string.IsNullOrWhiteSpace(app.Configuration["ASPNETCORE_HTTPS_PORTS"]))
        {
            app.UseHttpsRedirection();
        }

        app.UseAuthentication();
        app.UseMiddleware<Middleware.TenantStatusMiddleware>();
        app.UseAuthorization();
        app.UseRateLimiter();

        app.MapControllers();
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
        });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("ready"),
        });

        return app;
    }
}
