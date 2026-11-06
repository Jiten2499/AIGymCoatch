using FluentValidation;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Email;

public sealed record ActivationPreviewResponse(string FullName, string Email, DateTime ExpiresAt);

public sealed record GetActivationPreviewQuery(string Token) : IRequest<Result<ActivationPreviewResponse>>;

public sealed class GetActivationPreviewQueryValidator : AbstractValidator<GetActivationPreviewQuery>
{
    public GetActivationPreviewQueryValidator() => RuleFor(query => query.Token).NotEmpty().MaximumLength(500);
}

public sealed class GetActivationPreviewQueryHandler : IRequestHandler<GetActivationPreviewQuery, Result<ActivationPreviewResponse>>
{
    private readonly IPasswordResetRepository _resets;
    private readonly ISecretTokenFactory _tokens;
    private readonly IUserAccountRepository _users;

    public GetActivationPreviewQueryHandler(IPasswordResetRepository resets, ISecretTokenFactory tokens, IUserAccountRepository users)
    {
        _resets = resets;
        _tokens = tokens;
        _users = users;
    }

    public async Task<Result<ActivationPreviewResponse>> Handle(GetActivationPreviewQuery request, CancellationToken cancellationToken)
    {
        var reset = await _resets.GetActiveByHashAsync(_tokens.Hash(request.Token), cancellationToken).ConfigureAwait(false);
        if (reset is null)
        {
            return Result.Failure<ActivationPreviewResponse>(IdentityErrors.ResetInvalid);
        }

        var user = await _users.GetByIdAsync(reset.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null || user.Status != UserStatus.Active)
        {
            return Result.Failure<ActivationPreviewResponse>(IdentityErrors.ResetInvalid);
        }

        return Result.Success(new ActivationPreviewResponse($"{user.FirstName} {user.LastName}".Trim(), user.Email, reset.ExpiresAt));
    }
}

public sealed record ResendAccountActivationCommand(Guid UserId) : IRequest<Result>;

public sealed class ResendAccountActivationCommandHandler : IRequestHandler<ResendAccountActivationCommand, Result>
{
    private readonly ICurrentActor _actor;
    private readonly IPermissionService _permissions;
    private readonly ITenantRepository _tenants;
    private readonly ITenantMembershipRepository _members;
    private readonly IRoleRepository _roles;
    private readonly IAccountActivationMailer _mailer;

    public ResendAccountActivationCommandHandler(ICurrentActor actor, IPermissionService permissions, ITenantRepository tenants, ITenantMembershipRepository members, IRoleRepository roles, IAccountActivationMailer mailer)
    {
        _actor = actor;
        _permissions = permissions;
        _tenants = tenants;
        _members = members;
        _roles = roles;
        _mailer = mailer;
    }

    public async Task<Result> Handle(ResendAccountActivationCommand request, CancellationToken cancellationToken)
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

        var names = await _roles.GetRoleNamesAsync(request.UserId, tenantId, cancellationToken).ConfigureAwait(false);
        var role = names.Contains(RoleNames.Trainer, StringComparer.Ordinal)
            ? RoleNames.Trainer
            : names.Contains(RoleNames.Member, StringComparer.Ordinal) ? RoleNames.Member : names.FirstOrDefault() ?? RoleNames.Receptionist;
        var tenant = await _tenants.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return await _mailer.ResendAsync(request.UserId, tenantId, role, tenant?.Name ?? "LifeFit AI", cancellationToken).ConfigureAwait(false);
    }
}
