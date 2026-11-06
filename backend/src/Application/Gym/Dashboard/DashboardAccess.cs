using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Licensing;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Gym.Dashboard;

public interface IDashboardAccess
{
    Task<Result<DashboardWorkspace>> OpenAsync(CancellationToken cancellationToken);

    Task<Result<DashboardWorkspace>> OpenMemberAsync(CancellationToken cancellationToken);

    Task<Result<DashboardWorkspace>> OpenAreaAsync(string areaCode, CancellationToken cancellationToken);
}

public sealed record DashboardWorkspace(
    Tenant Tenant,
    BusinessProfile? Profile,
    TimeZoneInfo TimeZone,
    DateTime LocalNow,
    IReadOnlyList<string> Permissions,
    Guid UserId);

public sealed class DashboardAccess : IDashboardAccess
{
    private readonly ICurrentActor _actor;
    private readonly ITenantRepository _tenants;
    private readonly ITenantMembershipRepository _members;
    private readonly IRoleRepository _roles;
    private readonly IBusinessProfileRepository _profiles;
    private readonly IFeatureFlagService _features;
    private readonly IDateTimeProvider _clock;

    public DashboardAccess(
        ICurrentActor actor,
        ITenantRepository tenants,
        ITenantMembershipRepository members,
        IRoleRepository roles,
        IBusinessProfileRepository profiles,
        IFeatureFlagService features,
        IDateTimeProvider clock)
    {
        _actor = actor;
        _tenants = tenants;
        _members = members;
        _roles = roles;
        _profiles = profiles;
        _features = features;
        _clock = clock;
    }

    public async Task<Result<DashboardWorkspace>> OpenAsync(CancellationToken cancellationToken)
    {
        var opened = await OpenMemberAsync(cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return opened;
        }

        if (!opened.Value.Permissions.Contains(PermissionCodes.ReportRead, StringComparer.Ordinal))
        {
            return Result.Failure<DashboardWorkspace>(IdentityErrors.Forbidden);
        }

        return opened;
    }

    public async Task<Result<DashboardWorkspace>> OpenMemberAsync(CancellationToken cancellationToken)
    {
        if (_actor.UserId is not Guid userId || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<DashboardWorkspace>(IdentityErrors.InvalidCredentials);
        }

        var tenant = await _tenants.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (tenant is null)
        {
            return Result.Failure<DashboardWorkspace>(IdentityErrors.TenantNotFound);
        }

        if (!RoleNames.IsBusiness(tenant.TenantType))
        {
            return Result.Failure<DashboardWorkspace>(GymErrors.BusinessWorkspaceRequired);
        }

        if (tenant.Status != TenantStatus.Active)
        {
            return Result.Failure<DashboardWorkspace>(IdentityErrors.TenantUnavailable);
        }

        if (await _members.GetActiveAsync(tenantId, userId, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure<DashboardWorkspace>(IdentityErrors.NotMember);
        }

        var permissions = await _roles.GetPermissionCodesAsync(userId, tenantId, cancellationToken).ConfigureAwait(false);
        var profile = await _profiles.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var zone = TimeZoneInfo.Utc;
        if (profile is not null && TimeZoneInfo.TryFindSystemTimeZoneById(profile.TimeZone, out var found))
        {
            zone = found;
        }

        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc), zone);
        return Result.Success(new DashboardWorkspace(tenant, profile, zone, localNow, permissions, userId));
    }

    public async Task<Result<DashboardWorkspace>> OpenAreaAsync(string areaCode, CancellationToken cancellationToken)
    {
        var opened = await OpenAsync(cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return opened;
        }

        var area = DashboardAreas.All.First(item => item.Code == areaCode);
        if (!opened.Value.Permissions.Contains(area.Permission, StringComparer.Ordinal))
        {
            return Result.Failure<DashboardWorkspace>(IdentityErrors.Forbidden);
        }

        if (!await _features.IsEnabledAsync(opened.Value.Tenant.Id, area.Feature, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<DashboardWorkspace>(GymErrors.FeatureDisabled);
        }

        return opened;
    }
}
