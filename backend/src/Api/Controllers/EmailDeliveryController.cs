using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeFitAI.Api.Controllers;

[Authorize]
public sealed class EmailDeliveryController : ApiControllerBase
{
    public EmailDeliveryController(MediatR.ISender sender) : base(sender)
    {
    }

    [HttpGet("email/configuration")]
    public async Task<IActionResult> GetConfiguration(CancellationToken cancellationToken) =>
        (await Sender.Send(new GetEmailConfigurationQuery(), cancellationToken)).ToActionResult();

    [HttpPut("email/configuration")]
    public async Task<IActionResult> SaveConfiguration([FromBody] SaveEmailConfigurationBody body, CancellationToken cancellationToken) =>
        (await Sender.Send(new SaveEmailConfigurationCommand(
            body.Provider,
            body.SmtpHost,
            body.SmtpPort,
            body.Encryption,
            body.AuthenticationEnabled,
            body.Username,
            body.Password,
            body.SenderName,
            body.SenderEmail,
            body.ReplyToEmail,
            body.ConnectionTimeoutSeconds,
            body.MaxEmailsPerMinute,
            body.MaxDeliveryAttempts,
            body.RetryBaseDelaySeconds,
            body.IsEnabled,
            body.Activate), cancellationToken)).ToActionResult();

    [HttpPost("email/configuration/enabled")]
    public async Task<IActionResult> SetEnabled([FromBody] SetEmailEnabledBody body, CancellationToken cancellationToken) =>
        (await Sender.Send(new SetEmailProviderEnabledCommand(body.IsEnabled), cancellationToken)).ToActionResult();

    [HttpPost("email/configuration/test-connection")]
    public async Task<IActionResult> TestConnection(CancellationToken cancellationToken) =>
        (await Sender.Send(new TestEmailConnectionCommand(), cancellationToken)).ToActionResult();

    [HttpPost("email/configuration/test-email")]
    public async Task<IActionResult> SendTestEmail([FromBody] SendTestEmailBody body, CancellationToken cancellationToken) =>
        (await Sender.Send(new SendTestEmailCommand(body.Recipient), cancellationToken)).ToActionResult();

    [HttpGet("email/delivery-logs")]
    public async Task<IActionResult> ListLogs(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] string? templateKey,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken) =>
        (await Sender.Send(new ListEmailDeliveryLogsQuery(search, status, templateKey, fromUtc, toUtc, page ?? 1, pageSize ?? 20), cancellationToken)).ToActionResult();

    [HttpPost("email/delivery-logs/{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new RetryEmailDeliveryCommand(id), cancellationToken)).ToActionResult();
}

public sealed record SaveEmailConfigurationBody(
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
    bool Activate);

public sealed record SetEmailEnabledBody(bool IsEnabled);

public sealed record SendTestEmailBody(string Recipient);
