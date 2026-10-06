using FluentValidation;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Tenancy;

public sealed record CreateWorkspaceUserCommand(string Email, string FirstName, string LastName, string RoleName) : IRequest<Result<WorkspaceUserCreated>>;

public sealed class CreateWorkspaceUserCommandValidator : AbstractValidator<CreateWorkspaceUserCommand>
{
    public CreateWorkspaceUserCommandValidator()
    {
        RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(command => command.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.LastName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.RoleName).Must(WorkspaceRoleRules.CanAssign).WithMessage("Choose a business role. SuperAdmin cannot be assigned.");
    }
}

public sealed record UpdateWorkspaceUserCommand(Guid UserId, string FirstName, string LastName, UserStatus Status, string RoleName) : IRequest<Result>;

public sealed class UpdateWorkspaceUserCommandValidator : AbstractValidator<UpdateWorkspaceUserCommand>
{
    public UpdateWorkspaceUserCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.LastName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Status).IsInEnum();
        RuleFor(command => command.RoleName).Must(WorkspaceRoleRules.CanAssign).WithMessage("Choose a business role. SuperAdmin cannot be assigned.");
    }
}

public sealed record DeleteWorkspaceUsersCommand(IReadOnlyList<Guid> UserIds) : IRequest<Result>;

public sealed class DeleteWorkspaceUsersCommandValidator : AbstractValidator<DeleteWorkspaceUsersCommand>
{
    public DeleteWorkspaceUsersCommandValidator() => RuleFor(command => command.UserIds).NotEmpty();
}

public sealed class WorkspaceUserCommandHandler :
    IRequestHandler<CreateWorkspaceUserCommand, Result<WorkspaceUserCreated>>,
    IRequestHandler<UpdateWorkspaceUserCommand, Result>,
    IRequestHandler<DeleteWorkspaceUsersCommand, Result>
{
    private readonly ICurrentActor _actor;
    private readonly IPermissionService _permissions;
    private readonly ITenantRepository _tenants;
    private readonly IUserAccountRepository _users;
    private readonly IAccountLinker _accounts;
    private readonly ITenantMembershipRepository _members;
    private readonly IRoleRepository _roles;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public WorkspaceUserCommandHandler(
        ICurrentActor actor,
        IPermissionService permissions,
        ITenantRepository tenants,
        IUserAccountRepository users,
        IAccountLinker accounts,
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
        _users = users;
        _accounts = accounts;
        _members = members;
        _roles = roles;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<WorkspaceUserCreated>> Handle(CreateWorkspaceUserCommand request, CancellationToken cancellationToken)
    {
        var access = await WorkspaceAccess.OpenAsync(_actor, _tenants, _permissions, PermissionCodes.UserCreate, cancellationToken).ConfigureAwait(false);
        if (access.IsFailure)
        {
            return Result.Failure<WorkspaceUserCreated>(access.Error);
        }

        if (request.RoleName == RoleNames.SuperAdmin || !WorkspaceRoleRules.CanAssign(request.RoleName))
        {
            return Result.Failure<WorkspaceUserCreated>(IdentityErrors.SuperAdminRejected);
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var existing = await _users.GetByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        if (existing is not null && await _members.GetActiveAsync(access.Value.TenantId, existing.Id, cancellationToken).ConfigureAwait(false) is not null)
        {
            return Result.Failure<WorkspaceUserCreated>(IdentityErrors.MembershipConflict);
        }

        var role = await _roles.GetByNameAsync(request.RoleName, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            return Result.Failure<WorkspaceUserCreated>(IdentityErrors.RoleNotFound);
        }

        var password = existing is null ? TemporaryPassword.Create() : null;
        WorkspaceUserCreated? created = null;
        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var linked = await _accounts.ResolveAsync(email, password, request.FirstName, request.LastName, null, ct).ConfigureAwait(false);
                if (linked.IsFailure)
                {
                    throw new WorkspaceUserException(linked.Error);
                }

                var user = linked.Value;
                var now = _clock.UtcNow;
                if (await _members.GetActiveAsync(access.Value.TenantId, user.Id, ct).ConfigureAwait(false) is null)
                {
                    await _members.AddAsync(TenantMember.Create(Guid.NewGuid(), access.Value.TenantId, user.Id, now), ct).ConfigureAwait(false);
                }

                var names = await _roles.GetRoleNamesAsync(user.Id, access.Value.TenantId, ct).ConfigureAwait(false);
                if (!names.Contains(request.RoleName, StringComparer.Ordinal))
                {
                    await _roles.AddAsync(UserRole.Create(Guid.NewGuid(), user.Id, role.Id, access.Value.TenantId), ct).ConfigureAwait(false);
                }

                await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), access.Value.TenantId, access.Value.ActorId, "CreateWorkspaceUser", "TenantMember", user.Id, null, request.RoleName, _client.IpAddress, _client.UserAgent, now), ct).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
                created = new WorkspaceUserCreated(user.Id, user.Email, user.FirstName, user.LastName, request.RoleName, password);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (WorkspaceUserException exception)
        {
            return Result.Failure<WorkspaceUserCreated>(exception.Error);
        }

        return created is null
            ? Result.Failure<WorkspaceUserCreated>(IdentityErrors.RoleNotFound)
            : Result.Success(created);
    }

    public async Task<Result> Handle(UpdateWorkspaceUserCommand request, CancellationToken cancellationToken)
    {
        var access = await WorkspaceAccess.OpenAsync(_actor, _tenants, _permissions, PermissionCodes.UserUpdate, cancellationToken).ConfigureAwait(false);
        if (access.IsFailure)
        {
            return access;
        }

        if (request.RoleName == RoleNames.SuperAdmin || !WorkspaceRoleRules.CanAssign(request.RoleName))
        {
            return Result.Failure(IdentityErrors.SuperAdminRejected);
        }

        if (await _members.GetActiveAsync(access.Value.TenantId, request.UserId, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure(IdentityErrors.MemberNotFound);
        }

        var user = await _users.GetByIdAsync(request.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return Result.Failure(IdentityErrors.MemberNotFound);
        }

        var owner = request.UserId == access.Value.OwnerUserId;
        if (owner && request.RoleName != RoleNames.GymOwner)
        {
            return Result.Failure(IdentityErrors.OwnerRoleRejected);
        }

        if (request.Status == UserStatus.Disabled && user.Status != UserStatus.Disabled)
        {
            if (request.UserId == access.Value.ActorId)
            {
                return Result.Failure(IdentityErrors.SelfDisableRejected);
            }

            if (owner)
            {
                return Result.Failure(IdentityErrors.OwnerDisableRejected);
            }
        }

        var names = await _roles.GetRoleNamesAsync(request.UserId, access.Value.TenantId, cancellationToken).ConfigureAwait(false);
        if (!names.Contains(request.RoleName, StringComparer.Ordinal))
        {
            if (!await _permissions.HasPermissionAsync(access.Value.ActorId, access.Value.TenantId, PermissionCodes.RoleAssign, cancellationToken).ConfigureAwait(false))
            {
                return Result.Failure(IdentityErrors.Forbidden);
            }

            var role = await _roles.GetByNameAsync(request.RoleName, cancellationToken).ConfigureAwait(false);
            if (role is null)
            {
                return Result.Failure(IdentityErrors.RoleNotFound);
            }

            var now = _clock.UtcNow;
            await _roles.RemoveForMemberAsync(request.UserId, access.Value.TenantId, now, cancellationToken).ConfigureAwait(false);
            await _roles.AddAsync(UserRole.Create(Guid.NewGuid(), request.UserId, role.Id, access.Value.TenantId), cancellationToken).ConfigureAwait(false);
        }

        user.Rename(request.FirstName, request.LastName);
        if (request.Status == UserStatus.Disabled)
        {
            user.Disable();
        }
        else
        {
            user.Activate();
        }

        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), access.Value.TenantId, access.Value.ActorId, "UpdateWorkspaceUser", "User", user.Id, null, request.RoleName, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result> Handle(DeleteWorkspaceUsersCommand request, CancellationToken cancellationToken)
    {
        var access = await WorkspaceAccess.OpenAsync(_actor, _tenants, _permissions, PermissionCodes.TenantMemberRemove, cancellationToken).ConfigureAwait(false);
        if (access.IsFailure)
        {
            return access;
        }

        var ids = request.UserIds.Distinct().ToArray();
        foreach (var userId in ids)
        {
            if (userId == access.Value.OwnerUserId)
            {
                return Result.Failure(IdentityErrors.OwnerRemovalRejected);
            }

            if (userId == access.Value.ActorId)
            {
                return Result.Failure(IdentityErrors.SelfRemovalRejected);
            }

            if (await _members.GetActiveAsync(access.Value.TenantId, userId, cancellationToken).ConfigureAwait(false) is null)
            {
                return Result.Failure(IdentityErrors.MemberNotFound);
            }
        }

        var now = _clock.UtcNow;
        foreach (var userId in ids)
        {
            var member = await _members.GetActiveAsync(access.Value.TenantId, userId, cancellationToken).ConfigureAwait(false);
            member!.Remove(now);
            await _roles.RemoveForMemberAsync(userId, access.Value.TenantId, now, cancellationToken).ConfigureAwait(false);
            await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), access.Value.TenantId, access.Value.ActorId, "RemoveWorkspaceUser", "TenantMember", userId, null, null, _client.IpAddress, _client.UserAgent, now), cancellationToken).ConfigureAwait(false);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}

sealed class WorkspaceUserException : Exception
{
    public WorkspaceUserException(Error error) => Error = error;

    public Error Error { get; }
}
