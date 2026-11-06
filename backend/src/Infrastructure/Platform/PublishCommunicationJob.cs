using System.Text.Json;
using LifeFitAI.Application.Gym.Communications;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform.BackgroundJobs;
using LifeFitAI.Application.Platform.SystemJobs;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Infrastructure.Persistence;
using LifeFitAI.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LifeFitAI.Infrastructure.Platform;

public sealed class PublishCommunicationJob
{
    private readonly IServiceScopeFactory _scopes;

    public PublishCommunicationJob(IServiceScopeFactory scopes) => _scopes = scopes;

    public async Task RunAsync(Guid communicationId, Guid tenantId, string correlationId, DateTime publishAtUtc)
    {
        using var scope = _scopes.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<ScheduledCommunicationPublisher>();
        await publisher.ExecuteAsync(communicationId, tenantId, correlationId, publishAtUtc, CancellationToken.None).ConfigureAwait(false);
    }
}

public sealed class ScheduledCommunicationPublisher
{
    private readonly ApplicationDbContext _db;
    private readonly ICommunicationDelivery _delivery;
    private readonly IAuditLogRepository _audit;
    private readonly IDateTimeProvider _clock;

    public ScheduledCommunicationPublisher(ApplicationDbContext db, ICommunicationDelivery delivery, IAuditLogRepository audit, IDateTimeProvider clock)
    {
        _db = db;
        _delivery = delivery;
        _audit = audit;
        _clock = clock;
    }

    public async Task ExecuteAsync(Guid communicationId, Guid tenantId, string correlationId, DateTime publishAtUtc, CancellationToken cancellationToken)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(item => item.Id == tenantId, cancellationToken).ConfigureAwait(false);
        var communication = await _db.Communications.FirstOrDefaultAsync(item => item.Id == communicationId && item.TenantId == tenantId, cancellationToken).ConfigureAwait(false);
        if (tenant is null || tenant.Status != TenantStatus.Active || communication is null || communication.Status != CommunicationStatus.Scheduled)
        {
            return;
        }

        if (communication.PublishAt is not DateTime scheduled || Math.Abs((scheduled - publishAtUtc).TotalSeconds) > 1 || _clock.UtcNow < scheduled)
        {
            return;
        }

        if (communication.ExpiresAt is DateTime expiry && expiry <= _clock.UtcNow)
        {
            return;
        }

        communication.Publish(_clock.UtcNow);
        await _delivery.DeliverAsync(communication, _clock.UtcNow, cancellationToken).ConfigureAwait(false);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, null, "CommunicationPublishedFromSchedule", "Communication", communication.Id, null, communication.Title, null, null, _clock.UtcNow, correlationId), cancellationToken).ConfigureAwait(false);
        try
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another publish or cancel won the row version.
        }
    }
}

public sealed class OutboxDispatcher : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<OutboxDispatcher> _logger;

    public OutboxDispatcher(IServiceScopeFactory scopes, ILogger<OutboxDispatcher> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ConfigureAwait(false);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Outbox dispatch failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
        }
    }

    public async Task DispatchOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
        var scheduler = scope.ServiceProvider.GetRequiredService<IBackgroundJobScheduler>();
        var clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var pending = await store.ListPendingAsync(20, cancellationToken).ConfigureAwait(false);
        foreach (var message in pending)
        {
            if (message.MessageType == MembershipExpiryMessages.MessageType)
            {
                await MembershipExpiryDelivery.ApplyAsync(scope.ServiceProvider, message, clock.UtcNow, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (message.MessageType == "Chat.Message")
            {
                message.MarkDispatched(message.Payload, clock.UtcNow);
                continue;
            }

            if (message.MessageType == CircularPublication.MessageType)
            {
                await CircularPublishedDelivery.ApplyAsync(scope.ServiceProvider, message, clock.UtcNow, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (message.MessageType == LifeFitAI.Application.Email.EmailOutboxPayload.MessageType)
            {
                await LifeFitAI.Infrastructure.Email.EmailOutboxDelivery.ApplyAsync(scope.ServiceProvider, message, clock.UtcNow, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (message.MessageType != "Communication.Publish")
            {
                continue;
            }

            var payload = JsonSerializer.Deserialize<CommunicationPublishPayload>(message.Payload);
            if (payload is null)
            {
                message.Fail("The outbox payload could not be read.");
                continue;
            }

            var communicationId = payload.CommunicationId;
            var tenantId = payload.TenantId;
            var correlationId = payload.CorrelationId ?? "";
            var publishAt = payload.PublishAtUtc;
            var jobId = scheduler.Schedule<PublishCommunicationJob>(
                new BackgroundJobMetadata("Communication.Publish", "default", tenantId, correlationId, "Communication", communicationId.ToString("D"), null),
                job => job.RunAsync(communicationId, tenantId, correlationId, publishAt),
                message.AvailableAtUtc);
            message.MarkDispatched(JsonSerializer.Serialize(payload with { JobId = jobId }), clock.UtcNow);
        }

        if (pending.Count > 0)
        {
            await unit.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
