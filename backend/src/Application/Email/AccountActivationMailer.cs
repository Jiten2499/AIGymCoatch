using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Email;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace LifeFitAI.Application.Email;

public sealed record AccountActivationNotice(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string RoleName,
    Guid TenantId,
    string BusinessName,
    bool ExistingAccount);

public interface IPublicAppLinks
{
    string LoginUrl { get; }

    string ActivationUrl(string token);

    string MobileActivationUrl(string token);
}

public interface IAccountActivationMailer
{
    Task SendAsync(AccountActivationNotice notice, CancellationToken cancellationToken);

    Task<Result> ResendAsync(Guid userId, Guid tenantId, string roleName, string businessName, CancellationToken cancellationToken);
}

public sealed class AccountActivationMailer : IAccountActivationMailer
{
    public static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);

    private readonly IEmailService _email;
    private readonly IEmailTemplateRenderer _templates;
    private readonly IEmailConfigCache _configuration;
    private readonly IPasswordResetRepository _resets;
    private readonly ISecretTokenFactory _tokens;
    private readonly IUserAccountRepository _users;
    private readonly IPublicAppLinks _links;
    private readonly IDateTimeProvider _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AccountActivationMailer> _logger;

    public AccountActivationMailer(
        IEmailService email,
        IEmailTemplateRenderer templates,
        IEmailConfigCache configuration,
        IPasswordResetRepository resets,
        ISecretTokenFactory tokens,
        IUserAccountRepository users,
        IPublicAppLinks links,
        IDateTimeProvider clock,
        IUnitOfWork unitOfWork,
        ILogger<AccountActivationMailer> logger)
    {
        _email = email;
        _templates = templates;
        _configuration = configuration;
        _resets = resets;
        _tokens = tokens;
        _users = users;
        _links = links;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task SendAsync(AccountActivationNotice notice, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(notice.Email))
        {
            return;
        }

        try
        {
            if (notice.ExistingAccount)
            {
                await QueueAsync(notice, null, null, false, $"Invite:{notice.TenantId:N}:{notice.UserId:N}:{notice.RoleName}", cancellationToken).ConfigureAwait(false);
                return;
            }

            var raw = _tokens.Create();
            var tokenId = Guid.NewGuid();
            var expires = _clock.UtcNow.Add(InvitationLifetime);
            await _resets.AddAsync(PasswordResetToken.Create(tokenId, notice.UserId, _tokens.Hash(raw), _clock.UtcNow, expires), cancellationToken).ConfigureAwait(false);
            await QueueAsync(notice, raw, expires, true, $"Welcome:{notice.TenantId:N}:{notice.UserId:N}:{notice.RoleName}", cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Welcome email was not queued for {Email}", notice.Email);
        }
    }

    public async Task<Result> ResendAsync(Guid userId, Guid tenantId, string roleName, string businessName, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null || string.IsNullOrWhiteSpace(user.Email))
        {
            return Result.Failure(IdentityErrors.UserNotFound);
        }

        var raw = _tokens.Create();
        var tokenId = Guid.NewGuid();
        var now = _clock.UtcNow;
        await _resets.RevokeUnusedAsync(userId, now, cancellationToken).ConfigureAwait(false);
        await _resets.AddAsync(PasswordResetToken.Create(tokenId, user.Id, _tokens.Hash(raw), now, now.Add(InvitationLifetime)), cancellationToken).ConfigureAwait(false);
        var notice = new AccountActivationNotice(user.Id, user.Email, user.FirstName, user.LastName, roleName, tenantId, businessName, false);
        await QueueAsync(notice, raw, now.Add(InvitationLifetime), true, $"WelcomeResend:{tenantId:N}:{user.Id:N}:{tokenId:N}", cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    private async Task QueueAsync(AccountActivationNotice notice, string? rawToken, DateTime? expiresAt, bool activation, string idempotencyKey, CancellationToken cancellationToken)
    {
        var templateKey = TemplateKey(notice.RoleName, activation);
        var support = await SupportLineAsync(cancellationToken).ConfigureAwait(false);
        var values = new Dictionary<string, string>
        {
            ["name"] = $"{notice.FirstName} {notice.LastName}".Trim(),
            ["role"] = notice.RoleName,
            ["business"] = notice.BusinessName,
            ["email"] = notice.Email.Trim().ToLowerInvariant(),
            ["loginUrl"] = _links.LoginUrl,
            ["link"] = rawToken is null ? _links.LoginUrl : _links.ActivationUrl(rawToken),
            ["mobileLink"] = rawToken is null ? "" : _links.MobileActivationUrl(rawToken),
            ["expires"] = expiresAt?.ToString("u") ?? "",
            ["support"] = support,
        };
        var rendered = _templates.Render(templateKey, values);
        var queued = await _email.QueueAsync(
            new EmailMessageRequest(
                notice.Email.Trim(),
                rendered.Subject,
                rendered.HtmlBody,
                rendered.TextBody,
                templateKey,
                PlatformConstants.PlatformTenantId,
                notice.TenantId,
                EmailClassifications.Transactional,
                idempotencyKey,
                null),
            cancellationToken).ConfigureAwait(false);
        if (queued.IsFailure)
        {
            _logger.LogWarning("Account email {Template} was not queued for {Email}: {Code}", templateKey, notice.Email, queued.Error.Code);
        }
    }

    private async Task<string> SupportLineAsync(CancellationToken cancellationToken)
    {
        var configuration = await _configuration.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        var address = configuration?.ReplyToEmail ?? configuration?.SenderEmail;
        return string.IsNullOrWhiteSpace(address)
            ? "Contact your gym administrator if you need help."
            : $"Contact {address} if you need help.";
    }

    public static string TemplateKey(string roleName, bool activation)
    {
        if (!activation)
        {
            if (roleName == RoleNames.Trainer)
            {
                return EmailTemplateKeys.InvitationTrainer;
            }

            if (roleName == RoleNames.Member)
            {
                return EmailTemplateKeys.InvitationMember;
            }

            return EmailTemplateKeys.InvitationStaff;
        }

        if (roleName == RoleNames.Trainer)
        {
            return EmailTemplateKeys.WelcomeTrainer;
        }

        if (roleName == RoleNames.Member)
        {
            return EmailTemplateKeys.WelcomeMember;
        }

        if (roleName == RoleNames.GymOwner)
        {
            return EmailTemplateKeys.WelcomeOwner;
        }

        return EmailTemplateKeys.WelcomeStaff;
    }
}
