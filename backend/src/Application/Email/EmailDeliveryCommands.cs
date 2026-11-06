using FluentValidation;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Gym.Communications;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Email;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Domain.Platform;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;
using System.Text.Json;

namespace LifeFitAI.Application.Email;

public sealed record ListEmailDeliveryLogsQuery(
    string? Search,
    string? Status,
    string? TemplateKey,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int Page,
    int PageSize) : IRequest<Result<EmailDeliveryPage>>;

public sealed class ListEmailDeliveryLogsQueryValidator : AbstractValidator<ListEmailDeliveryLogsQuery>
{
    public ListEmailDeliveryLogsQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed record RetryEmailDeliveryCommand(Guid Id) : IRequest<Result<EmailDeliveryLogResponse>>;

public sealed class EmailDeliveryHandler :
    IRequestHandler<ListEmailDeliveryLogsQuery, Result<EmailDeliveryPage>>,
    IRequestHandler<RetryEmailDeliveryCommand, Result<EmailDeliveryLogResponse>>
{
    private readonly ICurrentActor _actor;
    private readonly IRoleRepository _roles;
    private readonly IPermissionService _permissions;
    private readonly IEmailDeliveryStore _store;
    private readonly IOutboxStore _outbox;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IAuditLogRepository _audit;
    private readonly IClientContext _client;

    public EmailDeliveryHandler(
        ICurrentActor actor,
        IRoleRepository roles,
        IPermissionService permissions,
        IEmailDeliveryStore store,
        IOutboxStore outbox,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IAuditLogRepository audit,
        IClientContext client)
    {
        _actor = actor;
        _roles = roles;
        _permissions = permissions;
        _store = store;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _audit = audit;
        _client = client;
    }

    public async Task<Result<EmailDeliveryPage>> Handle(ListEmailDeliveryLogsQuery request, CancellationToken cancellationToken)
    {
        if (await PlatformAccess.RequireSuperAdminAsync(_actor, _roles, _permissions, cancellationToken).ConfigureAwait(false) is { IsFailure: true } denied)
        {
            return Result.Failure<EmailDeliveryPage>(denied.Error);
        }

        var (items, total) = await _store.ListAsync(
            request.Search,
            request.Status,
            request.TemplateKey,
            request.FromUtc,
            request.ToUtc,
            request.Page,
            request.PageSize,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(new EmailDeliveryPage(items.Select(Map).ToArray(), request.Page, request.PageSize, total));
    }

    public async Task<Result<EmailDeliveryLogResponse>> Handle(RetryEmailDeliveryCommand request, CancellationToken cancellationToken)
    {
        if (await PlatformAccess.RequireSuperAdminAsync(_actor, _roles, _permissions, cancellationToken).ConfigureAwait(false) is { IsFailure: true } denied)
        {
            return Result.Failure<EmailDeliveryLogResponse>(denied.Error);
        }

        var log = await _store.GetAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (log is null)
        {
            return Result.Failure<EmailDeliveryLogResponse>(EmailErrors.DeliveryNotFound);
        }

        if (!log.CanRetry)
        {
            return Result.Failure<EmailDeliveryLogResponse>(EmailErrors.RetryNotAllowed);
        }

        log.ResetForRetry();
        var retryKey = $"{log.IdempotencyKey}:retry:{_clock.UtcNow.Ticks}";
        var payload = JsonSerializer.Serialize(new EmailOutboxPayload(log.Id, retryKey));
        var outbox = OutboxMessage.Create(
            Guid.NewGuid(),
            log.TenantId,
            EmailOutboxPayload.MessageType,
            payload,
            _clock.UtcNow,
            log.CorrelationId,
            retryKey,
            _clock.UtcNow);
        log.BindOutbox(outbox.Id);
        await _outbox.AddAsync(outbox, cancellationToken).ConfigureAwait(false);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), null, _actor.UserId, "EmailDeliveryRetried", "EmailDeliveryLog", log.Id, null, log.TemplateKey, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(Map(log));
    }

    private static EmailDeliveryLogResponse Map(EmailDeliveryLog log) =>
        new(
            log.Id,
            log.TenantId,
            log.RelatedTenantId,
            log.Recipient,
            log.Subject,
            log.TemplateKey,
            log.Provider,
            log.Status,
            log.Classification,
            log.AttemptCount,
            log.MaxAttempts,
            log.ProviderMessageId,
            log.FailureReason,
            log.QueuedAt,
            log.SentAt,
            log.CanRetry);
}

public sealed record EmailOutboxPayload(Guid DeliveryLogId, string IdempotencyKey)
{
    public const string MessageType = "Email.Send";
}
