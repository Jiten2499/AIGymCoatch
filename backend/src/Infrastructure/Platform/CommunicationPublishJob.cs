using LifeFitAI.Application.Gym.Communications;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.SharedKernel.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LifeFitAI.Infrastructure.Platform;

public sealed class CommunicationPublishJob : BackgroundService, IBackgroundJob
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<CommunicationPublishJob> _logger;

    public CommunicationPublishJob(IServiceScopeFactory scopes, ILogger<CommunicationPublishJob> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    public string Name => "communication-publish";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken).ConfigureAwait(false);
        while (!stoppingToken.IsCancellationRequested)
        {
            await ((IBackgroundJob)this).ExecuteAsync(stoppingToken).ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);
        }
    }

    async Task IBackgroundJob.ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var delivery = scope.ServiceProvider.GetRequiredService<ICommunicationDelivery>();
            var clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var published = await delivery.DeliverPublishedWithoutRecipientsAsync(clock.UtcNow, cancellationToken).ConfigureAwait(false);
            if (published > 0)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Scheduled communication publish failed");
        }
    }
}
