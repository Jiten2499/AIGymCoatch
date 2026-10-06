using LifeFitAI.Api.Contracts;
using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Gym.Business;
using LifeFitAI.Application.Gym.Dashboard;
using LifeFitAI.Application.Licensing;
using LifeFitAI.Application.Tenancy;
using LifeFitAI.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeFitAI.Api.Controllers;

[Authorize]
public sealed class BusinessController : ApiControllerBase
{
    public BusinessController(MediatR.ISender sender) : base(sender)
    {
    }

    [HttpGet("business/profile")]
    [RequiresPermission(PermissionCodes.BusinessProfileRead)]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken) =>
        (await Sender.Send(new GetBusinessProfileQuery(), cancellationToken)).ToActionResult();

    [HttpPut("business/profile")]
    [RequiresPermission(PermissionCodes.BusinessProfileUpdate)]
    public async Task<IActionResult> UpdateProfile(UpdateBusinessProfileRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new UpdateBusinessProfileCommand(request.LegalName, request.Description, request.Email, request.Phone, request.Website, request.Address, request.City, request.State, request.Country, request.PostalCode, request.LogoUrl, request.TimeZone, request.Currency), cancellationToken)).ToActionResult();

    [HttpGet("business/users")]
    [RequiresPermission(PermissionCodes.TenantMemberRead)]
    public async Task<IActionResult> Users(int? page, int? pageSize, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListWorkspaceUsersQuery(page ?? 1, pageSize ?? 20), cancellationToken)).ToActionResult();

    [HttpPost("business/users")]
    [RequiresPermission(PermissionCodes.UserCreate)]
    public async Task<IActionResult> CreateUser(CreateWorkspaceUserRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new CreateWorkspaceUserCommand(request.Email, request.FirstName, request.LastName, request.RoleName), cancellationToken)).ToActionResult();

    [HttpPut("business/users/{userId:guid}")]
    [RequiresPermission(PermissionCodes.UserUpdate)]
    public async Task<IActionResult> UpdateUser(Guid userId, UpdateWorkspaceUserRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new UpdateWorkspaceUserCommand(userId, request.FirstName, request.LastName, request.Status, request.RoleName), cancellationToken)).ToActionResult();

    [HttpGet("business/plans")]
    [RequiresPermission(PermissionCodes.LicenseRead)]
    public async Task<IActionResult> Plans(CancellationToken cancellationToken) =>
        (await Sender.Send(new ListWorkspacePlansQuery(), cancellationToken)).ToActionResult();

    [HttpPost("business/users/{userId:guid}/plan")]
    [RequiresPermission(PermissionCodes.LicenseManage)]
    public async Task<IActionResult> AssignPlan(Guid userId, AssignWorkspacePlanRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new AssignWorkspacePlanCommand(userId, request.SubscriptionId), cancellationToken)).ToActionResult();

    [HttpPost("business/users/delete")]
    [RequiresPermission(PermissionCodes.TenantMemberRemove)]
    public async Task<IActionResult> DeleteUsers(DeleteWorkspaceUsersRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new DeleteWorkspaceUsersCommand(request.UserIds), cancellationToken)).ToActionResult();

    [HttpGet("business/dashboard")]
    [RequiresPermission(PermissionCodes.ReportRead)]
    public async Task<IActionResult> Overview(CancellationToken cancellationToken) =>
        (await Sender.Send(new GetBusinessDashboardOverviewQuery(), cancellationToken)).ToActionResult();

    [HttpGet("business/dashboard/analytics/members")]
    [RequiresPermission(PermissionCodes.ReportRead)]
    public async Task<IActionResult> MemberAnalytics(DateOnly from, DateOnly to, AnalyticsPeriod period, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetMemberGrowthAnalyticsQuery(from, to, period), cancellationToken)).ToActionResult();

    [HttpGet("business/dashboard/analytics/attendance")]
    [RequiresPermission(PermissionCodes.ReportRead)]
    public async Task<IActionResult> AttendanceAnalytics(DateOnly from, DateOnly to, AnalyticsPeriod period, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetAttendanceAnalyticsQuery(from, to, period), cancellationToken)).ToActionResult();

    [HttpGet("business/dashboard/analytics/memberships")]
    [RequiresPermission(PermissionCodes.ReportRead)]
    public async Task<IActionResult> MembershipAnalytics(DateOnly from, DateOnly to, AnalyticsPeriod period, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetMembershipAnalyticsQuery(from, to, period), cancellationToken)).ToActionResult();

    [HttpGet("business/dashboard/insights")]
    [RequiresPermission(PermissionCodes.ReportRead)]
    public async Task<IActionResult> Insights(DateOnly from, DateOnly to, AnalyticsPeriod period, bool refresh, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetBusinessDashboardInsightsQuery(from, to, period, refresh), cancellationToken)).ToActionResult();

    [HttpGet("dashboard")]
    [RequiresPermission(PermissionCodes.ReportRead)]
    public async Task<IActionResult> Dashboard(Guid? branchId, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetBusinessDashboardQuery(branchId), cancellationToken)).ToActionResult();
}
