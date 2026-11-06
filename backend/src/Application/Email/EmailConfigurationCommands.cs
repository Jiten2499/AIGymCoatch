using FluentValidation;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Email;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Email;

public sealed record GetEmailConfigurationQuery : IRequest<Result<EmailConfigurationResponse>>;

public sealed record SaveEmailConfigurationCommand(
    string Provider,
    string SmtpHost,
    int SmtpPort,
    string Encryption,
    bool AuthenticationEnabled,
    string Username,
    string? Password,
    string SenderName,
    string SenderEmail,
    string? ReplyToEmail,
    int ConnectionTimeoutSeconds,
    int MaxEmailsPerMinute,
    int MaxDeliveryAttempts,
    int RetryBaseDelaySeconds,
    bool IsEnabled,
    bool Activate) : IRequest<Result<EmailConfigurationResponse>>;

public sealed class SaveEmailConfigurationCommandValidator : AbstractValidator<SaveEmailConfigurationCommand>
{
    public SaveEmailConfigurationCommandValidator()
    {
        RuleFor(command => command.Provider).Must(EmailProviders.IsImplemented).WithMessage(EmailErrors.ProviderUnsupported.Description);
        RuleFor(command => command.SmtpHost).NotEmpty().MaximumLength(255);
        RuleFor(command => command.SmtpPort).InclusiveBetween(1, 65535);
        RuleFor(command => command.Encryption).Must(value => value is EmailEncryption.SslTls or EmailEncryption.StartTls);
        RuleFor(command => command.Username).NotEmpty().MaximumLength(320).When(command => command.AuthenticationEnabled);
        RuleFor(command => command.Password).MaximumLength(500);
        RuleFor(command => command.SenderName).NotEmpty().MaximumLength(200);
        RuleFor(command => command.SenderEmail).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(command => command.ReplyToEmail).EmailAddress().MaximumLength(320).When(command => !string.IsNullOrWhiteSpace(command.ReplyToEmail));
        RuleFor(command => command.ConnectionTimeoutSeconds).InclusiveBetween(1, 120);
        RuleFor(command => command.MaxEmailsPerMinute).InclusiveBetween(1, 600);
        RuleFor(command => command.MaxDeliveryAttempts).InclusiveBetween(1, 20);
        RuleFor(command => command.RetryBaseDelaySeconds).InclusiveBetween(5, 3600);
    }
}

public sealed record SetEmailProviderEnabledCommand(bool IsEnabled) : IRequest<Result<EmailConfigurationResponse>>;

public sealed record TestEmailConnectionCommand : IRequest<Result<EmailTestResponse>>;

public sealed record SendTestEmailCommand(string Recipient) : IRequest<Result<EmailTestResponse>>;

public sealed class SendTestEmailCommandValidator : AbstractValidator<SendTestEmailCommand>
{
    public SendTestEmailCommandValidator() =>
        RuleFor(command => command.Recipient).NotEmpty().EmailAddress().MaximumLength(320);
}

public sealed class EmailConfigurationHandler :
    IRequestHandler<GetEmailConfigurationQuery, Result<EmailConfigurationResponse>>,
    IRequestHandler<SaveEmailConfigurationCommand, Result<EmailConfigurationResponse>>,
    IRequestHandler<SetEmailProviderEnabledCommand, Result<EmailConfigurationResponse>>,
    IRequestHandler<TestEmailConnectionCommand, Result<EmailTestResponse>>,
    IRequestHandler<SendTestEmailCommand, Result<EmailTestResponse>>
{
    private readonly ICurrentActor _actor;
    private readonly IRoleRepository _roles;
    private readonly IPermissionService _permissions;
    private readonly IEmailConfigStore _store;
    private readonly IEmailConfigCache _cache;
    private readonly IEmailSecretProtector _secrets;
    private readonly IEmailProviderResolver _resolver;
    private readonly IEmailService _email;
    private readonly IUserAccountRepository _users;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public EmailConfigurationHandler(
        ICurrentActor actor,
        IRoleRepository roles,
        IPermissionService permissions,
        IEmailConfigStore store,
        IEmailConfigCache cache,
        IEmailSecretProtector secrets,
        IEmailProviderResolver resolver,
        IEmailService email,
        IUserAccountRepository users,
        IAuditLogRepository audit,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IClientContext client)
    {
        _actor = actor;
        _roles = roles;
        _permissions = permissions;
        _store = store;
        _cache = cache;
        _secrets = secrets;
        _resolver = resolver;
        _email = email;
        _users = users;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<EmailConfigurationResponse>> Handle(GetEmailConfigurationQuery request, CancellationToken cancellationToken)
    {
        if (await PlatformAccess.RequireSuperAdminAsync(_actor, _roles, _permissions, cancellationToken).ConfigureAwait(false) is { IsFailure: true } denied)
        {
            return Result.Failure<EmailConfigurationResponse>(denied.Error);
        }

        var configuration = await _store.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(await MapAsync(configuration, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<EmailConfigurationResponse>> Handle(SaveEmailConfigurationCommand request, CancellationToken cancellationToken)
    {
        if (await PlatformAccess.RequireSuperAdminAsync(_actor, _roles, _permissions, cancellationToken).ConfigureAwait(false) is { IsFailure: true } denied)
        {
            return Result.Failure<EmailConfigurationResponse>(denied.Error);
        }

        if (!EmailProviders.IsImplemented(request.Provider))
        {
            return Result.Failure<EmailConfigurationResponse>(EmailErrors.ProviderUnsupported);
        }

        var configuration = await _store.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        var created = false;
        if (configuration is null)
        {
            configuration = EmailProviderConfiguration.CreateHostingerDefaults(Guid.NewGuid());
            created = true;
        }

        if (request.AuthenticationEnabled && string.IsNullOrWhiteSpace(request.Password) && !configuration.HasPassword)
        {
            return Result.Failure<EmailConfigurationResponse>(EmailErrors.SecretRequired);
        }

        configuration.UpdateSmtp(
            request.SmtpHost,
            request.SmtpPort,
            request.Encryption,
            request.AuthenticationEnabled,
            request.Username,
            request.SenderName,
            request.SenderEmail,
            request.ReplyToEmail,
            request.ConnectionTimeoutSeconds,
            request.MaxEmailsPerMinute,
            request.MaxDeliveryAttempts,
            request.RetryBaseDelaySeconds,
            request.IsEnabled);

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            configuration.ReplacePassword(_secrets.Protect(request.Password));
            await AuditAsync("EmailSecretReplaced", configuration.Id, "SMTP password updated", cancellationToken).ConfigureAwait(false);
        }

        if (request.Activate)
        {
            await _store.ClearOtherActiveAsync(configuration.Id, cancellationToken).ConfigureAwait(false);
            configuration.Activate();
        }

        if (created)
        {
            await _store.AddAsync(configuration, cancellationToken).ConfigureAwait(false);
            await AuditAsync("EmailConfigurationCreated", configuration.Id, configuration.Provider, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await AuditAsync("EmailConfigurationUpdated", configuration.Id, $"v{configuration.ConfigVersion}", cancellationToken).ConfigureAwait(false);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _cache.Invalidate();
        return Result.Success(await MapAsync(configuration, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<EmailConfigurationResponse>> Handle(SetEmailProviderEnabledCommand request, CancellationToken cancellationToken)
    {
        if (await PlatformAccess.RequireSuperAdminAsync(_actor, _roles, _permissions, cancellationToken).ConfigureAwait(false) is { IsFailure: true } denied)
        {
            return Result.Failure<EmailConfigurationResponse>(denied.Error);
        }

        var configuration = await _store.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        if (configuration is null)
        {
            return Result.Failure<EmailConfigurationResponse>(EmailErrors.NotFound);
        }

        configuration.SetEnabled(request.IsEnabled);
        await AuditAsync(request.IsEnabled ? "EmailProviderEnabled" : "EmailProviderDisabled", configuration.Id, configuration.Provider, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _cache.Invalidate();
        return Result.Success(await MapAsync(configuration, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<EmailTestResponse>> Handle(TestEmailConnectionCommand request, CancellationToken cancellationToken)
    {
        if (await PlatformAccess.RequireSuperAdminAsync(_actor, _roles, _permissions, cancellationToken).ConfigureAwait(false) is { IsFailure: true } denied)
        {
            return Result.Failure<EmailTestResponse>(denied.Error);
        }

        var configuration = await _store.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        if (configuration is null)
        {
            return Result.Failure<EmailTestResponse>(EmailErrors.NotConfigured);
        }

        var settings = ToSettings(configuration);
        if (settings is null)
        {
            return Result.Failure<EmailTestResponse>(EmailErrors.SecretUnreadable);
        }

        var provider = _resolver.Resolve(configuration.Provider);
        var result = await provider.TestConnectionAsync(settings, cancellationToken).ConfigureAwait(false);
        configuration.RecordConnectionTest(result.Succeeded, result.Message, _clock.UtcNow);
        await AuditAsync("EmailConnectionTested", configuration.Id, result.Succeeded ? "Success" : "Failed", cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _cache.Invalidate();
        return Result.Success(result with { TestedAt = configuration.LastTestedAt ?? _clock.UtcNow });
    }

    public async Task<Result<EmailTestResponse>> Handle(SendTestEmailCommand request, CancellationToken cancellationToken)
    {
        if (await PlatformAccess.RequireSuperAdminAsync(_actor, _roles, _permissions, cancellationToken).ConfigureAwait(false) is { IsFailure: true } denied)
        {
            return Result.Failure<EmailTestResponse>(denied.Error);
        }

        var configuration = await _store.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        if (configuration is null || !configuration.IsEnabled)
        {
            return Result.Failure<EmailTestResponse>(EmailErrors.Disabled);
        }

        var rendered = new EmailTemplateRenderer().Render(EmailTemplateKeys.TestEmail, new Dictionary<string, string>
        {
            ["recipient"] = request.Recipient.Trim(),
            ["provider"] = configuration.DisplayName,
            ["sender"] = configuration.SenderName,
        });

        var queued = await _email.QueueAsync(
            new EmailMessageRequest(
                request.Recipient.Trim(),
                rendered.Subject,
                rendered.HtmlBody,
                rendered.TextBody,
                EmailTemplateKeys.TestEmail,
                PlatformConstants.PlatformTenantId,
                null,
                EmailClassifications.Test,
                $"TestEmail:{request.Recipient.Trim().ToLowerInvariant()}:{_clock.UtcNow.Ticks}",
                null),
            cancellationToken).ConfigureAwait(false);

        if (queued.IsFailure)
        {
            return Result.Failure<EmailTestResponse>(queued.Error);
        }

        await AuditAsync("EmailTestQueued", configuration.Id, request.Recipient.Trim().ToLowerInvariant(), cancellationToken).ConfigureAwait(false);
        return Result.Success(new EmailTestResponse(true, configuration.HealthStatus, "Test email queued for delivery.", _clock.UtcNow));
    }

    private EmailProviderSettings? ToSettings(EmailProviderConfiguration configuration)
    {
        string? password = null;
        if (configuration.AuthenticationEnabled)
        {
            if (!configuration.HasPassword)
            {
                return null;
            }

            password = _secrets.Unprotect(configuration.ProtectedPassword!);
            if (password is null)
            {
                return null;
            }
        }

        return new EmailProviderSettings(
            configuration.Provider,
            configuration.SmtpHost,
            configuration.SmtpPort,
            configuration.Encryption,
            configuration.AuthenticationEnabled,
            configuration.Username,
            password,
            configuration.SenderName,
            configuration.SenderEmail,
            configuration.ReplyToEmail,
            configuration.ConnectionTimeoutSeconds);
    }

    private async Task<EmailConfigurationResponse> MapAsync(EmailProviderConfiguration? configuration, CancellationToken cancellationToken)
    {
        if (configuration is null)
        {
            var defaults = EmailProviderConfiguration.CreateHostingerDefaults(Guid.Empty);
            return new EmailConfigurationResponse(
                null,
                defaults.Provider,
                defaults.DisplayName,
                false,
                false,
                defaults.SmtpHost,
                defaults.SmtpPort,
                defaults.Encryption,
                defaults.AuthenticationEnabled,
                defaults.Username,
                false,
                defaults.SenderName,
                defaults.SenderEmail,
                defaults.ReplyToEmail,
                defaults.ConnectionTimeoutSeconds,
                defaults.MaxEmailsPerMinute,
                defaults.MaxDeliveryAttempts,
                defaults.RetryBaseDelaySeconds,
                0,
                null,
                null,
                null,
                null,
                null,
                "NotTested",
                null,
                null,
                _resolver.ListOptions(),
                RateWarning(defaults.MaxEmailsPerMinute));
        }

        string? updatedByName = null;
        if (configuration.UpdatedBy is Guid userId)
        {
            var user = await _users.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
            if (user is not null)
            {
                updatedByName = $"{user.FirstName} {user.LastName}".Trim();
            }
        }

        return new EmailConfigurationResponse(
            configuration.Id,
            configuration.Provider,
            configuration.DisplayName,
            configuration.IsEnabled,
            configuration.IsActive,
            configuration.SmtpHost,
            configuration.SmtpPort,
            configuration.Encryption,
            configuration.AuthenticationEnabled,
            configuration.Username,
            configuration.HasPassword,
            configuration.SenderName,
            configuration.SenderEmail,
            configuration.ReplyToEmail,
            configuration.ConnectionTimeoutSeconds,
            configuration.MaxEmailsPerMinute,
            configuration.MaxDeliveryAttempts,
            configuration.RetryBaseDelaySeconds,
            configuration.ConfigVersion,
            configuration.LastTestedAt,
            configuration.LastTestStatus,
            configuration.LastTestMessage,
            configuration.LastEmailSentAt,
            configuration.LastEmailStatus,
            configuration.HealthStatus,
            configuration.UpdatedAt,
            updatedByName,
            _resolver.ListOptions(),
            RateWarning(configuration.MaxEmailsPerMinute));
    }

    private static string? RateWarning(int maxPerMinute) =>
        maxPerMinute > 2 ? "Hostinger Business Email commonly limits about 100 messages per hour. Keep Max emails per minute at 2 or lower unless your plan allows more." : null;

    private Task AuditAsync(string action, Guid id, string value, CancellationToken cancellationToken) =>
        _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), null, _actor.UserId, action, "EmailConfiguration", id, null, value, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken);
}

public sealed class EmailTemplateRenderer : IEmailTemplateRenderer
{
    public (string Subject, string HtmlBody, string TextBody) Render(string templateKey, IReadOnlyDictionary<string, string> values)
    {
        var link = values.GetValueOrDefault("link") ?? "";
        var recipient = values.GetValueOrDefault("recipient") ?? "";
        var provider = values.GetValueOrDefault("provider") ?? "LifeFit AI";
        var sender = values.GetValueOrDefault("sender") ?? "LifeFit AI";

        if (templateKey is EmailTemplateKeys.WelcomeStaff or EmailTemplateKeys.WelcomeTrainer or EmailTemplateKeys.WelcomeMember or EmailTemplateKeys.WelcomeOwner
            or EmailTemplateKeys.InvitationStaff or EmailTemplateKeys.InvitationTrainer or EmailTemplateKeys.InvitationMember)
        {
            return AccountEmailLayout.Render(templateKey, values);
        }

        return templateKey switch
        {
            EmailTemplateKeys.TestEmail => (
                "LifeFit AI email delivery test",
                $"<p>Hello,</p><p>This is a test email from <strong>{Escape(sender)}</strong> via {Escape(provider)}.</p><p>Recipient: {Escape(recipient)}</p>",
                $"This is a test email from {sender} via {provider}. Recipient: {recipient}"),
            _ => (
                "LifeFit AI account access",
                $"<p>Use this secure link to set your password:</p><p><a href=\"{Escape(link)}\">{Escape(link)}</a></p><p>This link expires and can be used once. LifeFit AI never emails plaintext passwords.</p>",
                $"Set your password: {link}. This link expires and can be used once."),
        };
    }

    private static string Escape(string value) =>
        System.Net.WebUtility.HtmlEncode(value);
}
