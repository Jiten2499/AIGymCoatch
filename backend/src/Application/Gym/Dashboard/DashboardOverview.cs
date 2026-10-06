using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Licensing;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Dashboard;

public sealed record GetBusinessDashboardOverviewQuery : IRequest<Result<BusinessDashboardOverview>>;

public sealed record BusinessDashboardOverview(
    DashboardTenant Tenant,
    DashboardUserContext User,
    IReadOnlyList<DashboardCapability> Capabilities,
    DashboardSummary Summary);

public sealed record DashboardTenant(
    Guid Id,
    string Name,
    string TenantType,
    string Status,
    string? LogoUrl,
    string TimeZone,
    DateTime CurrentDate);

public sealed record DashboardUserContext(string DisplayName, IReadOnlyList<string> Roles);

public sealed record DashboardCapability(string Code, bool Enabled);

public sealed record DashboardSummary(
    MemberKpis? Members,
    AttendanceKpis? Attendance,
    MembershipKpis? Memberships,
    PeopleKpis? People);

public sealed record MemberKpis(int Total, int Active, int NewThisMonth);

public sealed record AttendanceKpis(int CheckInsToday);

public sealed record MembershipKpis(int Active, int ExpiringIn7Days, int ExpiringIn30Days, int Expired);

public sealed record PeopleKpis(int ActiveTrainers, int ActiveStaff);

public sealed record DashboardArea(string Code, string Permission, string Feature);

public static class DashboardAreas
{
    public static readonly IReadOnlyList<DashboardArea> All =
    [
        new("Members", PermissionCodes.MemberRead, FeatureCodes.MemberManagement),
        new("Attendance", PermissionCodes.AttendanceRead, FeatureCodes.Attendance),
        new("Memberships", PermissionCodes.MembershipRead, FeatureCodes.MembershipManagement),
        new("Fitness", PermissionCodes.WorkoutRead, FeatureCodes.WorkoutPlans),
        new("Trainers", PermissionCodes.TrainerRead, FeatureCodes.StaffManagement),
        new("AI", PermissionCodes.AiUse, FeatureCodes.AiCoach),
        new("Reports", PermissionCodes.ReportRead, FeatureCodes.BusinessReports),
        new("Challenges", PermissionCodes.ChallengeRead, FeatureCodes.WorkoutPlans),
    ];
}

public sealed class GetBusinessDashboardOverviewQueryHandler : IRequestHandler<GetBusinessDashboardOverviewQuery, Result<BusinessDashboardOverview>>
{
    private readonly IDashboardAccess _access;
    private readonly IUserAccountRepository _users;
    private readonly IRoleRepository _roles;
    private readonly IFeatureFlagService _features;
    private readonly IDashboardQuery _kpis;
    private readonly IDateTimeProvider _clock;

    public GetBusinessDashboardOverviewQueryHandler(
        IDashboardAccess access,
        IUserAccountRepository users,
        IRoleRepository roles,
        IFeatureFlagService features,
        IDashboardQuery kpis,
        IDateTimeProvider clock)
    {
        _access = access;
        _users = users;
        _roles = roles;
        _features = features;
        _kpis = kpis;
        _clock = clock;
    }

    public async Task<Result<BusinessDashboardOverview>> Handle(GetBusinessDashboardOverviewQuery request, CancellationToken cancellationToken)
    {
        var opened = await _access.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return Result.Failure<BusinessDashboardOverview>(opened.Error);
        }

        var workspace = opened.Value;
        var user = await _users.GetByIdAsync(workspace.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return Result.Failure<BusinessDashboardOverview>(IdentityErrors.Forbidden);
        }

        var granted = new HashSet<string>(workspace.Permissions, StringComparer.Ordinal);
        var capabilities = new List<DashboardCapability>(DashboardAreas.All.Count);
        foreach (var area in DashboardAreas.All)
        {
            var enabled = granted.Contains(area.Permission)
                && await _features.IsEnabledAsync(workspace.Tenant.Id, area.Feature, cancellationToken).ConfigureAwait(false);
            capabilities.Add(new DashboardCapability(area.Code, enabled));
        }

        var roleNames = await _roles.GetRoleNamesAsync(workspace.UserId, workspace.Tenant.Id, cancellationToken).ConfigureAwait(false);
        var summary = await SummarizeAsync(workspace.Tenant.Id, workspace.TimeZone, capabilities, cancellationToken).ConfigureAwait(false);
        var overview = new BusinessDashboardOverview(
            new DashboardTenant(workspace.Tenant.Id, workspace.Tenant.Name, workspace.Tenant.TenantType.ToString(), workspace.Tenant.Status.ToString(), workspace.Profile?.LogoUrl, workspace.TimeZone.Id, workspace.LocalNow),
            new DashboardUserContext($"{user.FirstName} {user.LastName}".Trim(), roleNames),
            capabilities,
            summary);
        return Result.Success(overview);
    }

    private async Task<DashboardSummary> SummarizeAsync(Guid tenantId, TimeZoneInfo zone, List<DashboardCapability> capabilities, CancellationToken cancellationToken)
    {
        var scope = new KpiScope(Enabled(capabilities, "Members"), Enabled(capabilities, "Attendance"), Enabled(capabilities, "Memberships"), Enabled(capabilities, "Trainers"));
        if (!scope.Any)
        {
            return new DashboardSummary(null, null, null, null);
        }

        var counts = await _kpis.CountAsync(tenantId, _clock.UtcNow, zone, scope, cancellationToken).ConfigureAwait(false);
        return new DashboardSummary(
            scope.Members ? new MemberKpis(counts.TotalMembers, counts.ActiveMembers, counts.NewMembersThisMonth) : null,
            scope.Attendance ? new AttendanceKpis(counts.CheckInsToday) : null,
            scope.Memberships ? new MembershipKpis(counts.ActiveMemberships, counts.ExpiringIn7Days, counts.ExpiringIn30Days, counts.ExpiredMemberships) : null,
            scope.People ? new PeopleKpis(counts.ActiveTrainers, counts.ActiveStaff) : null);
    }

    private static bool Enabled(List<DashboardCapability> capabilities, string code) =>
        capabilities.Exists(capability => capability.Code == code && capability.Enabled);
}
