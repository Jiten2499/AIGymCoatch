using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Licensing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeFitAI.Api.Controllers;

[Authorize]
public sealed class LicenseAdminController : ApiControllerBase
{
    public LicenseAdminController(MediatR.ISender sender) : base(sender)
    {
    }

    [HttpGet("licenses/pool")]
    public async Task<IActionResult> Pool(CancellationToken cancellationToken) =>
        (await Sender.Send(new GetLicensePoolQuery(), cancellationToken)).ToActionResult();

    [HttpGet("licenses/assignments")]
    public async Task<IActionResult> Assignments(CancellationToken cancellationToken) =>
        (await Sender.Send(new ListLicenseAssignmentsQuery(), cancellationToken)).ToActionResult();

    [HttpPost("licenses/assignments")]
    public async Task<IActionResult> Assign([FromBody] AssignLicenseBody body, CancellationToken cancellationToken) =>
        (await Sender.Send(new AssignUserLicenseCommand(body.UserId), cancellationToken)).ToActionResult();

    [HttpPost("licenses/assignments/{id:guid}/revoke")]
    public async Task<IActionResult> RevokeAssignment(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new RevokeUserLicenseCommand(id), cancellationToken)).ToActionResult();

    [HttpGet("platform/plans")]
    public async Task<IActionResult> Plans(CancellationToken cancellationToken) =>
        (await Sender.Send(new ListPlansQuery(), cancellationToken)).ToActionResult();

    [HttpPost("platform/plans")]
    public async Task<IActionResult> CreatePlan([FromBody] CreatePlanBody body, CancellationToken cancellationToken) =>
        (await Sender.Send(new CreatePlanCommand(body.Code, body.Name, body.Description, body.AppliesTo, body.SortOrder, body.BaseCapacity), cancellationToken)).ToActionResult();

    [HttpPut("platform/plans/{id:guid}")]
    public async Task<IActionResult> UpdatePlan(Guid id, [FromBody] UpdatePlanBody body, CancellationToken cancellationToken) =>
        (await Sender.Send(new UpdatePlanCommand(id, body.Name, body.Description, body.AppliesTo, body.SortOrder, body.BaseCapacity), cancellationToken)).ToActionResult();

    [HttpPost("platform/plans/{id:guid}/activate")]
    public async Task<IActionResult> ActivatePlan(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new ActivatePlanCommand(id), cancellationToken)).ToActionResult();

    [HttpPost("platform/plans/{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivatePlan(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new DeactivatePlanCommand(id), cancellationToken)).ToActionResult();

    [HttpPost("platform/plans/{id:guid}/retire")]
    public async Task<IActionResult> RetirePlan(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new RetirePlanCommand(id), cancellationToken)).ToActionResult();

    [HttpPut("platform/plans/{id:guid}/features")]
    public async Task<IActionResult> SetFeature(Guid id, [FromBody] PlanFeatureBody body, CancellationToken cancellationToken) =>
        (await Sender.Send(new SetPlanFeatureCommand(id, body.FeatureCode, body.Enabled), cancellationToken)).ToActionResult();

    [HttpPost("platform/licenses/{id:guid}/extend")]
    public async Task<IActionResult> Extend(Guid id, [FromBody] ExtendLicenseBody body, CancellationToken cancellationToken) =>
        (await Sender.Send(new ExtendTenantLicenseCommand(id, body.ExpiresAt), cancellationToken)).ToActionResult();

    [HttpPost("platform/licenses/{id:guid}/revoke")]
    public async Task<IActionResult> RevokeLicense(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new RevokeTenantLicenseCommand(id), cancellationToken)).ToActionResult();

    [HttpGet("platform/licenses/expiring")]
    public async Task<IActionResult> Expiring(int? withinDays, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListExpiringLicensesQuery(withinDays ?? 30), cancellationToken)).ToActionResult();

    [HttpGet("platform/features")]
    public async Task<IActionResult> Features(CancellationToken cancellationToken) =>
        (await Sender.Send(new ListFeatureCatalogQuery(), cancellationToken)).ToActionResult();

    [HttpGet("platform/plans/{id:guid}")]
    public async Task<IActionResult> Plan(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetPlanDetailQuery(id), cancellationToken)).ToActionResult();

    [HttpGet("platform/licenses")]
    public async Task<IActionResult> Licenses(int? page, int? pageSize, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListTenantLicensesQuery(page ?? 1, pageSize ?? 20), cancellationToken)).ToActionResult();

    [HttpGet("platform/licenses/{id:guid}")]
    public async Task<IActionResult> License(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetTenantLicenseQuery(id), cancellationToken)).ToActionResult();

    [HttpGet("platform/audit")]
    public async Task<IActionResult> Audit(int? page, int? pageSize, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListAuditQuery(page ?? 1, pageSize ?? 20), cancellationToken)).ToActionResult();

    [HttpGet("platform/tenants/{tenantId:guid}/members")]
    public async Task<IActionResult> Members(Guid tenantId, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListTenantMembersQuery(tenantId), cancellationToken)).ToActionResult();

    [HttpGet("platform/tenants/{tenantId:guid}/license-assignments")]
    public async Task<IActionResult> TenantAssignments(Guid tenantId, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListTenantAssignmentsQuery(tenantId), cancellationToken)).ToActionResult();

    [HttpPost("platform/tenants/{tenantId:guid}/plan")]
    public async Task<IActionResult> AssignTenantPlan(Guid tenantId, [FromBody] AssignPlanBody body, CancellationToken cancellationToken) =>
        (await Sender.Send(new AssignTenantPlanCommand(tenantId, body.PlanId), cancellationToken)).ToActionResult();

    [HttpPost("platform/users/{userId:guid}/plan")]
    public async Task<IActionResult> AssignUserPlan(Guid userId, [FromBody] AssignPlanBody body, CancellationToken cancellationToken) =>
        (await Sender.Send(new AssignUserPlanCommand(userId, body.PlanId), cancellationToken)).ToActionResult();

    [HttpPost("platform/tenants/{tenantId:guid}/license-assignments")]
    public async Task<IActionResult> AssignSeat(Guid tenantId, [FromBody] AssignLicenseBody body, CancellationToken cancellationToken) =>
        (await Sender.Send(new AssignPlatformSeatCommand(tenantId, body.UserId), cancellationToken)).ToActionResult();

    [HttpPost("platform/license-assignments/{id:guid}/revoke")]
    public async Task<IActionResult> RevokeSeat(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new RevokePlatformSeatCommand(id), cancellationToken)).ToActionResult();
}

public sealed record AssignLicenseBody(Guid UserId);

public sealed record AssignPlanBody(Guid PlanId);

public sealed record CreatePlanBody(string Code, string Name, string? Description, string AppliesTo, int SortOrder, int BaseCapacity);

public sealed record UpdatePlanBody(string Name, string? Description, string AppliesTo, int SortOrder, int BaseCapacity);

public sealed record PlanFeatureBody(string FeatureCode, bool Enabled);

public sealed record ExtendLicenseBody(DateTime? ExpiresAt);
