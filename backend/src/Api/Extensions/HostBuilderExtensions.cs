using Serilog;
using Serilog.Formatting.Compact;

namespace LifeFitAI.Api.Extensions;

public static class HostBuilderExtensions
{
    public static IHostBuilder UseLifeFitSerilog(this IHostBuilder host)
    {
        ArgumentNullException.ThrowIfNull(host);
        Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

        return host.UseSerilog((context, _, configuration) =>
        {
            var logDirectory = Path.Combine(context.HostingEnvironment.ContentRootPath, "logs");
            Directory.CreateDirectory(logDirectory);

            configuration
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext()
                .WriteTo.Console(new RenderedCompactJsonFormatter())
                .WriteTo.File(
                    new CompactJsonFormatter(),
                    Path.Combine(logDirectory, "lifefit-.json"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14,
                    shared: true);
        });
    }
}
