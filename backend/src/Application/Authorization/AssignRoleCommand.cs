using FluentValidation;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Authorization;

public sealed record AssignRoleCommand(Guid TenantId, Guid UserId, string RoleName) : IRequest<Result>;

public sealed class AssignRoleCommandValidator : AbstractValidator<AssignRoleCommand>
{
    public AssignRoleCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty();
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.RoleName).NotEmpty().MaximumLength(64);
    }
}

public sealed class AssignRoleCommandHandler : IRequestHandler<AssignRoleCommand, Result>
{
    private readonly ICurrentActor _actor;
    private readonly IPermissionService _permissions;
    private readonly ITenantMembershipRepository _members;
    private readonly IRoleRepository _roles;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public AssignRoleCommandHandler(
        ICurrentActor actor,
        IPermissionService permissions,
        ITenantMembershipRepository members,
        IRoleRepository roles,
        IAuditLogRepository audit,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IClientContext client)
    {
        _actor = actor;
        _permissions = permissions;
        _members = members;
        _roles = roles;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result> Handle(AssignRoleCommand request, CancellationToken cancellationToken)
    {
        if (_actor.UserId is not Guid actorId)
        {
            return Result.Failure(IdentityErrors.InvalidCredentials);
        }

        if (!await _permissions.HasPermissionAsync(actorId, request.TenantId, PermissionCodes.RoleAssign, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(IdentityErrors.Forbidden);
        }

        if (request.RoleName == RoleNames.SuperAdmin)
        {
            return Result.Failure(IdentityErrors.SuperAdminRejected);
        }

        if (await _members.GetActiveAsync(request.TenantId, request.UserId, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure(IdentityErrors.NotMember);
        }

        var role = await _roles.GetByNameAsync(request.RoleName, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            return Result.Failure(IdentityErrors.RoleNotFound);
        }

        var existing = await _roles.GetRoleNamesAsync(request.UserId, request.TenantId, cancellationToken).ConfigureAwait(false);
        if (existing.Contains(request.RoleName, StringComparer.Ordinal))
        {
            return Result.Success();
        }

        var now = _clock.UtcNow;
        await _roles.AddAsync(UserRole.Create(Guid.NewGuid(), request.UserId, role.Id, request.TenantId), cancellationToken).ConfigureAwait(false);
        await _audit.AddAsync(
            AuditLog.Create(Guid.NewGuid(), request.TenantId, actorId, "AssignRole", "UserRole", request.UserId, null, request.RoleName, _client.IpAddress, _client.UserAgent, now),
            cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}
