using System.Text.Json;
using LifeFitAI.Application.Email;
using LifeFitAI.Domain.Email;
using LifeFitAI.Domain.Platform;
using LifeFitAI.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LifeFitAI.Infrastructure.Email;

public static class EmailOutboxDelivery
{
    public static async Task ApplyAsync(IServiceProvider services, OutboxMessage message, DateTime utcNow, CancellationToken cancellationToken)
    {
        if (message.AvailableAtUtc > utcNow)
        {
            return;
        }

        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("EmailOutboxDelivery");
        var envelope = JsonSerializer.Deserialize<EmailOutboxEnvelope>(message.Payload);
        if (envelope is null)
        {
            message.Fail("The email outbox payload could not be read.");
            message.Cancel(utcNow);
            return;
        }

        var db = services.GetRequiredService<ApplicationDbContext>();
        var log = await db.EmailDeliveryLogs.FindAsync([envelope.DeliveryLogId], cancellationToken).ConfigureAwait(false);
        if (log is null)
        {
            message.Fail("The email delivery log was not found.");
            message.Cancel(utcNow);
            return;
        }

        if (log.Status is EmailDeliveryStatuses.Accepted or EmailDeliveryStatuses.Delivered)
        {
            message.MarkDispatched(message.Payload, utcNow);
            return;
        }

        var cache = services.GetRequiredService<IEmailConfigCache>();
        var secrets = services.GetRequiredService<IEmailSecretProtector>();
        var resolver = services.GetRequiredService<IEmailProviderResolver>();
        var configuration = await cache.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        if (configuration is null || !configuration.IsEnabled)
        {
            log.MarkFailed(EmailErrors.Disabled.Description);
            message.Fail(EmailErrors.Disabled.Description);
            message.Cancel(utcNow);
            return;
        }

        string? password = null;
        if (configuration.AuthenticationEnabled)
        {
            if (!configuration.HasPassword)
            {
                log.MarkFailed(EmailErrors.SecretRequired.Description);
                message.Fail(EmailErrors.SecretRequired.Description);
                message.Cancel(utcNow);
                return;
            }

            password = secrets.Unprotect(configuration.ProtectedPassword!);
            if (password is null)
            {
                log.MarkFailed(EmailErrors.SecretUnreadable.Description);
                message.Fail(EmailErrors.SecretUnreadable.Description);
                message.Cancel(utcNow);
                return;
            }
        }

        var settings = new EmailProviderSettings(
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

        log.MarkProcessing();
        var provider = resolver.Resolve(configuration.Provider);
        EmailSendResult result;
        try
        {
            result = await provider.SendAsync(settings, envelope.Message, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Email provider threw while sending {DeliveryLogId}", log.Id);
            result = new EmailSendResult(false, null, "The email provider failed unexpectedly.", true);
        }

        if (result.Succeeded)
        {
            log.MarkAccepted(result.ProviderMessageId, utcNow);
            configuration.RecordEmailSent(true, utcNow);
            message.MarkDispatched(message.Payload, utcNow);
            return;
        }

        var reason = result.Error ?? "Email delivery failed.";
        configuration.RecordEmailSent(false, utcNow);
        if (result.Transient && log.AttemptCount < log.MaxAttempts)
        {
            var delay = TimeSpan.FromSeconds(configuration.RetryBaseDelaySeconds * Math.Max(1, log.AttemptCount));
            log.MarkRetrying(reason);
            message.Defer(reason, utcNow.Add(delay));
            return;
        }

        log.MarkFailed(reason);
        message.Fail(reason);
        message.Cancel(utcNow);
    }
}
