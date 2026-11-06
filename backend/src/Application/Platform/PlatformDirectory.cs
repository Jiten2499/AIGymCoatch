using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Platform;

public sealed record PlatformUserResponse(Guid Id, string Email, string FirstName, string LastName, string Status, Guid PersonalTenantId, string PersonalTenantName, DateTime CreatedAt, string Plan, bool HasPhoto = false);

public sealed record DirectoryScope(bool PlatformWide, Guid? TenantId, bool StaffOnly = false);

public static class DirectoryRules
{
    public const string ReservedTestDomain = "@example.com";

    public static bool IsRealAccount(string email) =>
        !email.EndsWith(ReservedTestDomain, StringComparison.OrdinalIgnoreCase);
}

public sealed record ListPlatformUsersQuery(int Page, int PageSize) : IRequest<Result<PageResult<PlatformUserResponse>>>;

public sealed class PlatformDirectoryHandler : IRequestHandler<ListPlatformUsersQuery, Result<PageResult<PlatformUserResponse>>>
{
    private readonly ICurrentActor _actor;
    private readonly IRoleRepository _roles;
    private readonly IPermissionService _permissions;
    private readonly IUserAccountRepository _users;

    public PlatformDirectoryHandler(ICurrentActor actor, IRoleRepository roles, IPermissionService permissions, IUserAccountRepository users)
    {
        _actor = actor;
        _roles = roles;
        _permissions = permissions;
        _users = users;
    }

    public async Task<Result<PageResult<PlatformUserResponse>>> Handle(ListPlatformUsersQuery request, CancellationToken cancellationToken)
    {
        if (_actor.UserId is not Guid userId || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<PageResult<PlatformUserResponse>>(IdentityErrors.InvalidCredentials);
        }

        var platform = await PlatformAccess.RequireSuperAdminAsync(_actor, _roles, _permissions, cancellationToken).ConfigureAwait(false);
        var scope = platform.IsSuccess
            ? new DirectoryScope(true, null)
            : await _permissions.HasPermissionAsync(userId, tenantId, PermissionCodes.TenantMemberRead, cancellationToken).ConfigureAwait(false)
                ? new DirectoryScope(false, tenantId)
                : null;
        if (scope is null)
        {
            return Result.Failure<PageResult<PlatformUserResponse>>(IdentityErrors.Forbidden);
        }

        var page = new PageQuery(request.Page, request.PageSize);
        var items = await _users.ListPageAsync(scope, page.Skip, page.NormalizedSize, cancellationToken).ConfigureAwait(false);
        var total = await _users.CountAsync(scope, cancellationToken).ConfigureAwait(false);
        return Result.Success(new PageResult<PlatformUserResponse>(items, page.NormalizedPage, page.NormalizedSize, total));
    }
}
