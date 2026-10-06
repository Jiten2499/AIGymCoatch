using LifeFitAI.Application.Platform;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace LifeFitAI.Infrastructure.Platform;

public static class TelemetryRegistration
{
    public static IServiceCollection AddPlatformTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(ObservabilityOptions.SectionName).Get<ObservabilityOptions>() ?? new ObservabilityOptions();
        if (!options.Enabled)
        {
            return services;
        }

        services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation();
                metrics.AddHttpClientInstrumentation();
                metrics.AddMeter(PlatformMetrics.MeterName);
                if (Uri.TryCreate(options.OtlpEndpoint, UriKind.Absolute, out var endpoint))
                {
                    metrics.AddOtlpExporter(exporter => exporter.Endpoint = endpoint);
                }
            })
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation();
                tracing.AddHttpClientInstrumentation();
                tracing.AddEntityFrameworkCoreInstrumentation();
                if (Uri.TryCreate(options.OtlpEndpoint, UriKind.Absolute, out var endpoint))
                {
                    tracing.AddOtlpExporter(exporter => exporter.Endpoint = endpoint);
                }
            });

        return services;
    }
}
