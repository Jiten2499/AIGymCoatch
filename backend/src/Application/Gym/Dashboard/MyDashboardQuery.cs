using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Licensing;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Dashboard;

public sealed record GetMyDashboardQuery : IRequest<Result<MyDashboardResponse>>;

public sealed record MyDashboardResponse(
    string Layout,
    string DisplayName,
    IReadOnlyList<MyDashboardWidget> Widgets,
    IReadOnlyList<DashboardQuickAction> Actions);

public sealed record MyDashboardWidget(string Code, string Title, string Value, string Detail, string? Href);

public sealed record DashboardActorContext(
    Guid TenantId,
    Guid UserId,
    Guid? TrainerProfileId,
    Guid? BranchId,
    bool PaymentsAll,
    string Currency);

public sealed record DashboardMetric(string Value, string Detail);

public interface IMyDashboardReader
{
    Task<DashboardActorContext> DescribeAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, DashboardMetric>> ReadAsync(
        DashboardActorContext context,
        bool limitToBranch,
        bool followUpsAll,
        IReadOnlyList<string> permissions,
        IReadOnlyCollection<string> widgetCodes,
        DateTime utcNow,
        TimeZoneInfo timeZone,
        CancellationToken cancellationToken);
}

public sealed class GetMyDashboardQueryHandler : IRequestHandler<GetMyDashboardQuery, Result<MyDashboardResponse>>
{
    private readonly IDashboardAccess _access;
    private readonly IUserAccountRepository _users;
    private readonly IRoleRepository _roles;
    private readonly IFeatureFlagService _features;
    private readonly ISubscriptionService _subscriptions;
    private readonly IMemberAccessScope _memberScope;
    private readonly IMyDashboardReader _reader;
    private readonly IDateTimeProvider _clock;

    public GetMyDashboardQueryHandler(
        IDashboardAccess access,
        IUserAccountRepository users,
        IRoleRepository roles,
        IFeatureFlagService features,
        ISubscriptionService subscriptions,
        IMemberAccessScope memberScope,
        IMyDashboardReader reader,
        IDateTimeProvider clock)
    {
        _access = access;
        _users = users;
        _roles = roles;
        _features = features;
        _subscriptions = subscriptions;
        _memberScope = memberScope;
        _reader = reader;
        _clock = clock;
    }

    public async Task<Result<MyDashboardResponse>> Handle(GetMyDashboardQuery request, CancellationToken cancellationToken)
    {
        var opened = await _access.OpenMemberAsync(cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return Result.Failure<MyDashboardResponse>(opened.Error);
        }

        var workspace = opened.Value;
        var user = await _users.GetByIdAsync(workspace.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return Result.Failure<MyDashboardResponse>(IdentityErrors.Forbidden);
        }

        var roles = await _roles.GetRoleNamesAsync(workspace.UserId, workspace.Tenant.Id, cancellationToken).ConfigureAwait(false);
        var layout = MyDashboardCatalog.LayoutFor(roles);
        var name = $"{user.FirstName} {user.LastName}".Trim();
        if (layout == MyDashboardCatalog.OwnerLayout)
        {
            return Result.Success(new MyDashboardResponse(layout, name, [], []));
        }

        if (!await _subscriptions.IsUsableAsync(workspace.Tenant.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<MyDashboardResponse>(GymErrors.FeatureDisabled);
        }

        var enabled = await EnabledFeaturesAsync(workspace, cancellationToken).ConfigureAwait(false);
        var permissions = workspace.Permissions.ToHashSet(StringComparer.Ordinal);
        var visible = MyDashboardCatalog.Visible(layout, permissions, enabled);
        var context = await _reader.DescribeAsync(workspace.Tenant.Id, workspace.UserId, cancellationToken).ConfigureAwait(false);
        var limit = await _memberScope.LimitAsync(workspace.Tenant.Id, workspace.UserId, cancellationToken).ConfigureAwait(false);
        var metrics = await _reader.ReadAsync(
            context,
            limit.Scope != AccessScopes.All && context.BranchId is not null,
            layout == MyDashboardCatalog.ManagerLayout,
            workspace.Permissions,
            visible.Select(widget => widget.Code).ToArray(),
            _clock.UtcNow,
            workspace.TimeZone,
            cancellationToken).ConfigureAwait(false);
        var widgets = visible.Select(widget =>
        {
            var metric = metrics.TryGetValue(widget.Code, out var found) ? found : new DashboardMetric("0", "No figures for this period.");
            return new MyDashboardWidget(widget.Code, widget.Title, metric.Value, metric.Detail, widget.Href);
        }).ToArray();
        var showActions = MyDashboardCatalog.WidgetVisibilityConfigured(permissions)
            ? permissions.Contains(PermissionCodes.DashboardWidgetQuickActions)
            : layout is MyDashboardCatalog.ReceptionistLayout or MyDashboardCatalog.ManagerLayout;
        var actions = showActions
            ? MyDashboardCatalog.Actions.Where(action => permissions.Contains(action.Permission)).ToArray()
            : [];
        return Result.Success(new MyDashboardResponse(layout, name, widgets, actions));
    }

    private async Task<HashSet<string>> EnabledFeaturesAsync(DashboardWorkspace workspace, CancellationToken cancellationToken)
    {
        var enabled = new HashSet<string>(StringComparer.Ordinal);
        foreach (var feature in MyDashboardCatalog.Widgets.Select(widget => widget.Feature).Where(feature => feature is not null).Distinct(StringComparer.Ordinal))
        {
            if (await _features.IsEnabledAsync(workspace.Tenant.Id, feature!, cancellationToken).ConfigureAwait(false))
            {
                enabled.Add(feature!);
            }
        }

        return enabled;
    }
}
