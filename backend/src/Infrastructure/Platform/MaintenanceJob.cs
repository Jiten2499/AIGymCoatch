using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.SharedKernel.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LifeFitAI.Infrastructure.Platform;

public sealed class TokenCleanupJob : BackgroundService, IBackgroundJob
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<TokenCleanupJob> _logger;

    public TokenCleanupJob(IServiceScopeFactory scopes, ILogger<TokenCleanupJob> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    public string Name => "refresh-token-cleanup";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);
        while (!stoppingToken.IsCancellationRequested)
        {
            await ((IBackgroundJob)this).ExecuteAsync(stoppingToken).ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromHours(6), stoppingToken).ConfigureAwait(false);
        }
    }

    async Task IBackgroundJob.ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var tokens = scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();
            var clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var removed = await tokens.PurgeExpiredAsync(clock.UtcNow, cancellationToken).ConfigureAwait(false);
            if (removed > 0)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            PlatformMetrics.Jobs.Add(1, new KeyValuePair<string, object?>("job", Name), new KeyValuePair<string, object?>("outcome", "success"));
            _logger.LogInformation("Refresh token cleanup removed {Count} expired or revoked records", removed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            PlatformMetrics.Jobs.Add(1, new KeyValuePair<string, object?>("job", Name), new KeyValuePair<string, object?>("outcome", "failure"));
            _logger.LogWarning("Refresh token cleanup failed");
        }
    }
}
