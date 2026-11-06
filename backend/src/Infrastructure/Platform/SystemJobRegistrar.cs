using LifeFitAI.Application.Platform.BackgroundJobs;
using LifeFitAI.Application.Platform.SystemJobs;
using LifeFitAI.Domain.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LifeFitAI.Infrastructure.Platform;

public sealed class SystemJobRegistrar : IHostedService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<SystemJobRegistrar> _logger;

    public SystemJobRegistrar(IServiceScopeFactory scopes, ILogger<SystemJobRegistrar> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var catalog = scope.ServiceProvider.GetRequiredService<ISystemJobCatalog>();
            var scheduler = scope.ServiceProvider.GetRequiredService<IBackgroundJobScheduler>();
            foreach (var job in await catalog.ListAsync(cancellationToken).ConfigureAwait(false))
            {
                scheduler.SyncRecurring(SystemJobMapping.Registration(job));
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "System job registration failed");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
