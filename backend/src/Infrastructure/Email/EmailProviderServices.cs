using System.Security.Cryptography;
using System.Text.Json;
using LifeFitAI.Application.Email;
using LifeFitAI.Application.Gym.Communications;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Email;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Domain.Platform;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LifeFitAI.Infrastructure.Email;

public sealed class DataProtectionEmailSecretProtector : IEmailSecretProtector
{
    private readonly IDataProtector _protector;

    public DataProtectionEmailSecretProtector(IDataProtectionProvider provider) =>
        _protector = provider.CreateProtector("LifeFitAI.Email.SmtpPassword");

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string? Unprotect(string protectedValue)
    {
        try
        {
            return _protector.Unprotect(protectedValue);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}

public sealed class EmailProviderResolver : IEmailProviderResolver
{
    private readonly HostingerSmtpEmailProvider _smtp;

    public EmailProviderResolver(HostingerSmtpEmailProvider smtp) => _smtp = smtp;

    public IEmailProvider Resolve(string provider) =>
        EmailProviders.IsSmtp(provider) ? _smtp : throw new InvalidOperationException(EmailErrors.ProviderUnsupported.Description);

    public IReadOnlyList<EmailProviderOption> ListOptions() =>
    [
        new(EmailProviders.SmtpHostinger, "SMTP (Hostinger)", true),
        new(EmailProviders.SendGrid, "SendGrid", false),
        new(EmailProviders.AmazonSes, "Amazon SES", false),
        new(EmailProviders.SmtpGeneric, "Other SMTP", false),
    ];
}

public sealed class OutboxEmailService : IEmailService
{
    private readonly IEmailDeliveryStore _logs;
    private readonly IEmailConfigCache _cache;
    private readonly IOutboxStore _outbox;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;

    public OutboxEmailService(
        IEmailDeliveryStore logs,
        IEmailConfigCache cache,
        IOutboxStore outbox,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock)
    {
        _logs = logs;
        _cache = cache;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<Guid>> QueueAsync(EmailMessageRequest request, CancellationToken cancellationToken)
    {
        var existing = await _logs.FindByIdempotencyAsync(request.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return Result.Success(existing.Id);
        }

        var configuration = await _cache.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        if (configuration is null || !configuration.IsEnabled)
        {
            return Result.Failure<Guid>(EmailErrors.Disabled);
        }

        var log = EmailDeliveryLog.Create(
            Guid.NewGuid(),
            request.TenantId == Guid.Empty ? PlatformConstants.PlatformTenantId : request.TenantId,
            request.RelatedTenantId,
            request.Recipient,
            request.Subject,
            request.TemplateKey,
            configuration.Provider,
            request.Classification,
            configuration.MaxDeliveryAttempts,
            request.IdempotencyKey,
            request.CorrelationId,
            _clock.UtcNow);

        var envelope = new EmailOutboxEnvelope(log.Id, request);
        var payload = JsonSerializer.Serialize(envelope);
        var outbox = OutboxMessage.Create(
            Guid.NewGuid(),
            log.TenantId,
            EmailOutboxPayload.MessageType,
            payload,
            _clock.UtcNow,
            request.CorrelationId,
            request.IdempotencyKey,
            _clock.UtcNow);
        log.BindOutbox(outbox.Id);
        await _logs.AddAsync(log, cancellationToken).ConfigureAwait(false);
        await _outbox.AddAsync(outbox, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(log.Id);
    }
}

public sealed record EmailOutboxEnvelope(Guid DeliveryLogId, EmailMessageRequest Message);

public sealed class PublicAppEmailOptions
{
    public const string SectionName = "Email";

    public string PublicAppBaseUrl { get; set; } = "http://localhost:3000";
}

public sealed class QueuedPasswordResetNotifier : Application.Identity.IPasswordResetNotifier
{
    private readonly IEmailService _email;
    private readonly IEmailTemplateRenderer _templates;
    private readonly IOptions<PublicAppEmailOptions> _options;
    private readonly ILogger<QueuedPasswordResetNotifier> _logger;

    public QueuedPasswordResetNotifier(
        IEmailService email,
        IEmailTemplateRenderer templates,
        IOptions<PublicAppEmailOptions> options,
        ILogger<QueuedPasswordResetNotifier> logger)
    {
        _email = email;
        _templates = templates;
        _options = options;
        _logger = logger;
    }

    public Task NotifyAsync(string email, string rawToken, CancellationToken cancellationToken) =>
        NotifyAsync(email, rawToken, EmailTemplateKeys.PasswordReset, null, cancellationToken);

    public async Task NotifyAsync(string email, string rawToken, string templateKey, Guid? relatedTenantId, CancellationToken cancellationToken)
    {
        var baseUrl = (_options.Value.PublicAppBaseUrl ?? "http://localhost:3000").TrimEnd('/');
        var link = $"{baseUrl}/reset-password?token={Uri.EscapeDataString(rawToken)}";
        var key = string.IsNullOrWhiteSpace(templateKey) ? EmailTemplateKeys.PasswordReset : templateKey.Trim();
        var rendered = _templates.Render(key, new Dictionary<string, string> { ["link"] = link, ["recipient"] = email });
        var result = await _email.QueueAsync(
            new EmailMessageRequest(
                email,
                rendered.Subject,
                rendered.HtmlBody,
                rendered.TextBody,
                key,
                PlatformConstants.PlatformTenantId,
                relatedTenantId,
                EmailClassifications.Transactional,
                $"{key}:{email.Trim().ToLowerInvariant()}:{rawToken.GetHashCode(StringComparison.Ordinal)}",
                null),
            cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            _logger.LogWarning("Account email {Template} was not queued for {Email}: {Code}", key, email, result.Error.Code);
        }
    }
}
