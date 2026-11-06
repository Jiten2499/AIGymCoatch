using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Gym.Configuration;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeFitAI.Api.Controllers;

[Authorize]
public sealed class ConfigurationController : ApiControllerBase
{
    public ConfigurationController(MediatR.ISender sender) : base(sender)
    {
    }

    [HttpGet("configuration")]
    [RequiresPermission(PermissionCodes.ConfigurationRead)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        (await Sender.Send(new GetBusinessConfigurationQuery(), cancellationToken)).ToActionResult();

    [HttpPut("configuration/membership")]
    [RequiresPermission(PermissionCodes.ConfigurationManage)]
    public async Task<IActionResult> Membership(MembershipConfigRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new UpdateMembershipConfigCommand(request.AllowOverlappingMemberships, request.AllowEarlyRenewal, request.DefaultStartOnJoinDate, request.GracePeriodDays, request.AutoExpireEnabled), cancellationToken)).ToActionResult();

    [HttpPut("configuration/freeze")]
    [RequiresPermission(PermissionCodes.ConfigurationManage)]
    public async Task<IActionResult> Freeze(FreezeConfigRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new UpdateFreezeConfigCommand(request.MaxFreezeDays, request.MinFreezeDays, request.MaxFreezeOccurrences, request.FreezeRequiresApproval, request.MaxExtensionDays, request.ExtensionRequiresApproval), cancellationToken)).ToActionResult();

    [HttpPut("configuration/attendance")]
    [RequiresPermission(PermissionCodes.ConfigurationManage)]
    public async Task<IActionResult> Attendance(AttendanceConfigRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new UpdateAttendanceConfigCommand(request.DuplicateCheckInMinutes, request.OpeningTime, request.ClosingTime, request.CheckInGraceMinutes, request.DefaultCheckInMethod), cancellationToken)).ToActionResult();

    [HttpPut("configuration/crm")]
    [RequiresPermission(PermissionCodes.ConfigurationManage)]
    public async Task<IActionResult> Crm(CrmConfigRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new UpdateCrmConfigCommand(request.DefaultTrialDays, request.FollowUpReminderHours, request.DefaultFollowUpDays), cancellationToken)).ToActionResult();

    [HttpPost("configuration/member-categories")]
    [RequiresPermission(PermissionCodes.ConfigurationManage)]
    public async Task<IActionResult> CreateCategory(MemberCategoryRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new CreateMemberCategoryCommand(request.Name, request.Description), cancellationToken)).ToActionResult();

    [HttpPut("configuration/member-categories/{id:guid}")]
    [RequiresPermission(PermissionCodes.ConfigurationManage)]
    public async Task<IActionResult> UpdateCategory(Guid id, MemberCategoryRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new UpdateMemberCategoryCommand(id, request.Name, request.Description), cancellationToken)).ToActionResult();

    [HttpPost("configuration/member-categories/{id:guid}/status")]
    [RequiresPermission(PermissionCodes.ConfigurationManage)]
    public async Task<IActionResult> CategoryStatus(Guid id, StatusRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new ChangeMemberCategoryStatusCommand(id, request.Active), cancellationToken)).ToActionResult();

    [HttpPost("configuration/discounts")]
    [RequiresPermission(PermissionCodes.ConfigurationManage)]
    public async Task<IActionResult> CreateDiscount(DiscountRuleRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new CreateDiscountRuleCommand(request.Name, request.Description, request.DiscountType, request.Value, request.AppliesTo, request.PackageId, request.PackageCategory, request.MemberCategoryId, request.MinimumPackagePrice, request.MinimumDurationDays, request.MaximumDiscountAmount, request.StartDate, request.EndDate), cancellationToken)).ToActionResult();

    [HttpPut("configuration/discounts/{id:guid}")]
    [RequiresPermission(PermissionCodes.ConfigurationManage)]
    public async Task<IActionResult> UpdateDiscount(Guid id, DiscountRuleRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new UpdateDiscountRuleCommand(id, request.Name, request.Description, request.DiscountType, request.Value, request.AppliesTo, request.PackageId, request.PackageCategory, request.MemberCategoryId, request.MinimumPackagePrice, request.MinimumDurationDays, request.MaximumDiscountAmount, request.StartDate, request.EndDate), cancellationToken)).ToActionResult();

    [HttpPost("configuration/discounts/{id:guid}/status")]
    [RequiresPermission(PermissionCodes.ConfigurationManage)]
    public async Task<IActionResult> DiscountStatus(Guid id, StatusRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new ChangeDiscountStatusCommand(id, request.Active), cancellationToken)).ToActionResult();

    [HttpPost("configuration/crm/lead-sources")]
    [RequiresPermission(PermissionCodes.ConfigurationManage)]
    public async Task<IActionResult> CreateLeadSource(CrmCatalogRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new CreateCrmLeadSourceCommand(request.Name), cancellationToken)).ToActionResult();

    [HttpPost("configuration/crm/lead-sources/{id:guid}/status")]
    [RequiresPermission(PermissionCodes.ConfigurationManage)]
    public async Task<IActionResult> LeadSourceStatus(Guid id, StatusRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new ChangeCrmLeadSourceStatusCommand(id, request.Active), cancellationToken)).ToActionResult();

    [HttpPost("configuration/crm/lost-reasons")]
    [RequiresPermission(PermissionCodes.ConfigurationManage)]
    public async Task<IActionResult> CreateLostReason(CrmCatalogRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new CreateCrmLostReasonCommand(request.Name), cancellationToken)).ToActionResult();

    [HttpPost("configuration/crm/lost-reasons/{id:guid}/status")]
    [RequiresPermission(PermissionCodes.ConfigurationManage)]
    public async Task<IActionResult> LostReasonStatus(Guid id, StatusRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new ChangeCrmLostReasonStatusCommand(id, request.Active), cancellationToken)).ToActionResult();
}

public sealed record MembershipConfigRequest(bool AllowOverlappingMemberships, bool AllowEarlyRenewal, bool DefaultStartOnJoinDate, int GracePeriodDays, bool AutoExpireEnabled);
public sealed record FreezeConfigRequest(int MaxFreezeDays, int MinFreezeDays, int MaxFreezeOccurrences, bool FreezeRequiresApproval, int MaxExtensionDays, bool ExtensionRequiresApproval);
public sealed record AttendanceConfigRequest(int DuplicateCheckInMinutes, string? OpeningTime, string? ClosingTime, int CheckInGraceMinutes, CheckInMethod DefaultCheckInMethod);
public sealed record CrmConfigRequest(int DefaultTrialDays, int FollowUpReminderHours, int DefaultFollowUpDays);
public sealed record MemberCategoryRequest(string Name, string? Description);
public sealed record StatusRequest(bool Active);
public sealed record CrmCatalogRequest(string Name);
public sealed record DiscountRuleRequest(
    string Name,
    string? Description,
    DiscountType DiscountType,
    decimal Value,
    DiscountAppliesTo AppliesTo,
    Guid? PackageId,
    PackageCategory? PackageCategory,
    Guid? MemberCategoryId,
    decimal? MinimumPackagePrice,
    int? MinimumDurationDays,
    decimal? MaximumDiscountAmount,
    DateOnly? StartDate,
    DateOnly? EndDate);
