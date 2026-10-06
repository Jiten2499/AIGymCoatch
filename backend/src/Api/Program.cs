using LifeFitAI.Api.Extensions;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.WebHost.ConfigureKestrel(options =>
    {
        var bytes = builder.Configuration.GetValue("Security:MaxRequestBytes", 1_048_576);
        options.Limits.MaxRequestBodySize = Math.Clamp(bytes, 1024, 10_485_760);
        options.Limits.MaxRequestHeadersTotalSize = 32 * 1024;
        options.Limits.MaxRequestLineSize = 8 * 1024;
    });
    builder.Host.UseLifeFitSerilog();
    builder.Services.AddLifeFitPlatform(builder.Configuration, builder.Environment);

    var app = builder.Build();
    app.UseLifeFitPlatform();
    app.Run();
}
catch (Exception exception)
{
    Log.Fatal(exception, "LifeFit AI API terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

public partial class Program;
