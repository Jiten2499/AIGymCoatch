using FluentValidation;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Tenancy;

public sealed record AddTenantMemberCommand(Guid TenantId, string Email, string RoleName) : IRequest<Result>;

public sealed class AddTenantMemberCommandValidator : AbstractValidator<AddTenantMemberCommand>
{
    public AddTenantMemberCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty();
        RuleFor(command => command.Email).NotEmpty().EmailAddress();
        RuleFor(command => command.RoleName).NotEmpty().MaximumLength(64);
    }
}

public sealed class AddTenantMemberCommandHandler : IRequestHandler<AddTenantMemberCommand, Result>
{
    private readonly ICurrentActor _actor;
    private readonly IPermissionService _permissions;
    private readonly ITenantMembershipRepository _members;
    private readonly IUserAccountRepository _users;
    private readonly IRoleRepository _roles;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public AddTenantMemberCommandHandler(
        ICurrentActor actor,
        IPermissionService permissions,
        ITenantMembershipRepository members,
        IUserAccountRepository users,
        IRoleRepository roles,
        IAuditLogRepository audit,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IClientContext client)
    {
        _actor = actor;
        _permissions = permissions;
        _members = members;
        _users = users;
        _roles = roles;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result> Handle(AddTenantMemberCommand request, CancellationToken cancellationToken)
    {
        if (_actor.UserId is not Guid actorId)
        {
            return Result.Failure(IdentityErrors.InvalidCredentials);
        }

        if (!await _permissions.HasPermissionAsync(actorId, request.TenantId, PermissionCodes.TenantMemberInvite, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(IdentityErrors.Forbidden);
        }

        if (request.RoleName == RoleNames.SuperAdmin)
        {
            return Result.Failure(IdentityErrors.SuperAdminRejected);
        }

        var user = await _users.GetByEmailAsync(request.Email.Trim().ToLowerInvariant(), cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return Result.Failure(IdentityErrors.InvalidCredentials);
        }

        if (await _members.GetActiveAsync(request.TenantId, user.Id, cancellationToken).ConfigureAwait(false) is not null)
        {
            return Result.Failure(IdentityErrors.MembershipConflict);
        }

        var role = await _roles.GetByNameAsync(request.RoleName, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            return Result.Failure(IdentityErrors.RoleNotFound);
        }

        var now = _clock.UtcNow;
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _members.AddAsync(TenantMember.Create(Guid.NewGuid(), request.TenantId, user.Id, now), ct).ConfigureAwait(false);
            await _roles.AddAsync(UserRole.Create(Guid.NewGuid(), user.Id, role.Id, request.TenantId), ct).ConfigureAwait(false);
            await _audit.AddAsync(
                AuditLog.Create(Guid.NewGuid(), request.TenantId, actorId, "AddMember", "TenantMember", user.Id, null, request.RoleName, _client.IpAddress, _client.UserAgent, now),
                ct).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}

public sealed record RemoveTenantMemberCommand(Guid TenantId, Guid UserId) : IRequest<Result>;

public sealed class RemoveTenantMemberCommandHandler : IRequestHandler<RemoveTenantMemberCommand, Result>
{
    private readonly ICurrentActor _actor;
    private readonly IPermissionService _permissions;
    private readonly ITenantRepository _tenants;
    private readonly ITenantMembershipRepository _members;
    private readonly IRoleRepository _roles;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public RemoveTenantMemberCommandHandler(
        ICurrentActor actor,
        IPermissionService permissions,
        ITenantRepository tenants,
        ITenantMembershipRepository members,
        IRoleRepository roles,
        IAuditLogRepository audit,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IClientContext client)
    {
        _actor = actor;
        _permissions = permissions;
        _tenants = tenants;
        _members = members;
        _roles = roles;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result> Handle(RemoveTenantMemberCommand request, CancellationToken cancellationToken)
    {
        if (_actor.UserId is not Guid actorId)
        {
            return Result.Failure(IdentityErrors.InvalidCredentials);
        }

        if (!await _permissions.HasPermissionAsync(actorId, request.TenantId, PermissionCodes.TenantMemberRemove, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(IdentityErrors.Forbidden);
        }

        var tenant = await _tenants.GetAsync(request.TenantId, cancellationToken).ConfigureAwait(false);
        if (tenant is null)
        {
            return Result.Failure(IdentityErrors.TenantNotFound);
        }

        if (tenant.OwnerUserId == request.UserId)
        {
            return Result.Failure(IdentityErrors.OwnerRemovalRejected);
        }

        var member = await _members.GetActiveAsync(request.TenantId, request.UserId, cancellationToken).ConfigureAwait(false);
        if (member is null)
        {
            return Result.Failure(IdentityErrors.MemberNotFound);
        }

        var now = _clock.UtcNow;
        member.Remove(now);
        await _roles.RemoveForMemberAsync(request.UserId, request.TenantId, now, cancellationToken).ConfigureAwait(false);
        await _audit.AddAsync(
            AuditLog.Create(Guid.NewGuid(), request.TenantId, actorId, "RemoveMember", "TenantMember", request.UserId, null, null, _client.IpAddress, _client.UserAgent, now),
            cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}
