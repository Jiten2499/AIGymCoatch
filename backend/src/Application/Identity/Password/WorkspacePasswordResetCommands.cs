using FluentValidation;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Email;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Identity.Password;

public sealed record SendWorkspacePasswordResetCommand(Guid UserId) : IRequest<Result>;

public sealed class SendWorkspacePasswordResetCommandValidator : AbstractValidator<SendWorkspacePasswordResetCommand>
{
    public SendWorkspacePasswordResetCommandValidator() => RuleFor(command => command.UserId).NotEmpty();
}

public sealed class SendWorkspacePasswordResetCommandHandler : IRequestHandler<SendWorkspacePasswordResetCommand, Result>
{
    private readonly ICurrentActor _actor;
    private readonly IPermissionService _permissions;
    private readonly ITenantMembershipRepository _members;
    private readonly IUserAccountRepository _users;
    private readonly IPasswordResetRepository _resets;
    private readonly ISecretTokenFactory _tokens;
    private readonly IPasswordResetNotifier _notifier;
    private readonly ITokenSettings _settings;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public SendWorkspacePasswordResetCommandHandler(
        ICurrentActor actor,
        IPermissionService permissions,
        ITenantMembershipRepository members,
        IUserAccountRepository users,
        IPasswordResetRepository resets,
        ISecretTokenFactory tokens,
        IPasswordResetNotifier notifier,
        ITokenSettings settings,
        IAuditLogRepository audit,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IClientContext client)
    {
        _actor = actor;
        _permissions = permissions;
        _members = members;
        _users = users;
        _resets = resets;
        _tokens = tokens;
        _notifier = notifier;
        _settings = settings;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result> Handle(SendWorkspacePasswordResetCommand request, CancellationToken cancellationToken)
    {
        if (_actor.UserId is not Guid actorId || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure(IdentityErrors.Forbidden);
        }

        if (!await _permissions.HasPermissionAsync(actorId, tenantId, PermissionCodes.UserUpdate, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(IdentityErrors.Forbidden);
        }

        if (await _members.GetActiveAsync(tenantId, request.UserId, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure(IdentityErrors.MemberNotFound);
        }

        var user = await _users.GetByIdAsync(request.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null || user.Status != UserStatus.Active)
        {
            return Result.Failure(IdentityErrors.MemberNotFound);
        }

        var now = _clock.UtcNow;
        var raw = _tokens.Create();
        await _resets.RevokeUnusedAsync(user.Id, now, cancellationToken).ConfigureAwait(false);
        await _resets.AddAsync(
            PasswordResetToken.Create(Guid.NewGuid(), user.Id, _tokens.Hash(raw), now, now.Add(_settings.ResetLifetime)),
            cancellationToken).ConfigureAwait(false);
        await _audit.AddAsync(
            AuditLog.Create(Guid.NewGuid(), tenantId, actorId, "SendPasswordReset", "User", user.Id, null, user.Email, _client.IpAddress, _client.UserAgent, now),
            cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await _notifier.NotifyAsync(user.Email, raw, EmailTemplateKeys.PasswordReset, tenantId, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}
