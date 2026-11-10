using LifeFitAI.Api.Contracts;
using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Members;
using LifeFitAI.Application.Gym.Payments;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeFitAI.Api.Controllers;

[Authorize]
public sealed class MembersController : ApiControllerBase
{
    public MembersController(MediatR.ISender sender) : base(sender)
    {
    }

    [HttpGet("members")]
    [RequiresPermission(PermissionCodes.MemberRead)]
    public async Task<IActionResult> Search(string? term, ProfileStatus? status, Guid? branchId, GymMembershipStatus? membershipStatus, int? page, int? pageSize, string? sort, CancellationToken cancellationToken) =>
        (await Sender.Send(new SearchMembersQuery(term, status, branchId, membershipStatus, page ?? 1, pageSize ?? 20, sort), cancellationToken)).ToActionResult();

    [HttpPost("members")]
    [RequiresPermission(PermissionCodes.MemberCreate)]
    public async Task<IActionResult> Create(CreateMemberRequest request, CancellationToken cancellationToken)
    {
        InitialMembershipPayment? payment = null;
        if (request.PaymentAmount is decimal amount && request.PaymentMethod is MembershipPaymentMethod method && request.PaymentDate is DateOnly paymentDate)
        {
            payment = new InitialMembershipPayment(amount, method, paymentDate, request.PaymentReferenceNumber, request.PaymentNotes);
        }

        return (await Sender.Send(new CreateMemberCommand(request.Email, request.Password, request.FirstName, request.LastName, request.Phone, request.BranchId, request.PlanId, request.StartDate, request.AutoRenew, request.DateOfBirth, request.Gender, request.EmergencyContactName, request.EmergencyContactPhone, request.EmergencyContactRelationship, request.Address, request.City, request.State, request.PostalCode, request.JoinDate, request.FitnessGoal, request.AssignedTrainerId, ProfileStatus.Active, request.AssignAppAccess, request.AppSubscriptionId, request.EnableBasicAppAccess, payment, request.PtPackageId, request.SendActivationLink), cancellationToken)).ToActionResult();
    }

    [HttpGet("members/{id:guid}")]
    [RequiresPermission(PermissionCodes.MemberRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetMemberQuery(id), cancellationToken)).ToActionResult();

    [HttpGet("members/{id:guid}/app-access")]
    [RequiresPermission(PermissionCodes.MemberRead)]
    public async Task<IActionResult> GetAppAccess(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetMemberAppAccessQuery(id), cancellationToken)).ToActionResult();

    [HttpPost("members/{id:guid}/basic-access")]
    [RequiresPermission(PermissionCodes.MemberUpdate)]
    public async Task<IActionResult> EnableBasicAccess(Guid id, EnableMemberBasicAccessRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new EnableMemberBasicAccessCommand(id, request.Email), cancellationToken)).ToActionResult();

    [HttpPost("members/{id:guid}/app-access")]
    [RequiresPermission(PermissionCodes.MemberUpdate)]
    public async Task<IActionResult> AssignAppPlan(Guid id, AssignMemberAppAccessRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new AssignMemberAppPlanCommand(id, request.SubscriptionId, request.Email), cancellationToken)).ToActionResult();

    [HttpDelete("members/{id:guid}/app-access")]
    [RequiresPermission(PermissionCodes.MemberUpdate)]
    public async Task<IActionResult> RemoveAppPlan(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new RemoveMemberAppPlanCommand(id), cancellationToken)).ToActionResult();

    [HttpPut("members/{id:guid}")]
    [RequiresPermission(PermissionCodes.MemberUpdate)]
    public async Task<IActionResult> Update(Guid id, UpdateMemberRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new UpdateMemberCommand(id, request.FirstName, request.LastName, request.Phone, request.DateOfBirth, request.Gender, request.EmergencyContactName, request.EmergencyContactPhone, request.EmergencyContactRelationship, request.Address, request.City, request.State, request.PostalCode, request.JoinDate, request.FitnessGoal, request.AssignedTrainerId, request.Status), cancellationToken)).ToActionResult();

    [HttpPatch("members/{id:guid}/status")]
    [RequiresPermission(PermissionCodes.MemberUpdate)]
    public async Task<IActionResult> ChangeStatus(Guid id, ChangeMemberStatusRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new ChangeMemberStatusCommand(id, request.Status), cancellationToken)).ToActionResult();

    [HttpDelete("members/{id:guid}")]
    [RequiresPermission(PermissionCodes.MemberDelete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new DeleteMemberProfileCommand(id), cancellationToken)).ToActionResult();

    [HttpGet("members/me/picture")]
    public async Task<IActionResult> MyPicture(CancellationToken cancellationToken) =>
        (await Sender.Send(new GetMyMemberPictureQuery(), cancellationToken)).ToActionResult();

    [HttpGet("members/me/profile-photo")]
    public async Task<IActionResult> MyPhoto(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetMyMemberPhotoQuery(), cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        Response.Headers.CacheControl = "private, no-store";
        return File(result.Value.Content, result.Value.ContentType);
    }

    [HttpGet("members/{id:guid}/profile-photo")]
    [RequiresPermission(PermissionCodes.MemberRead)]
    public async Task<IActionResult> Photo(Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetMemberPhotoQuery(id), cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        Response.Headers.CacheControl = "private, no-store";
        return File(result.Value.Content, result.Value.ContentType);
    }

    [HttpPost("members/{id:guid}/profile-photo")]
    [RequiresPermission(PermissionCodes.MemberUpdate)]
    [RequestSizeLimit(2_097_152)]
    [RequestFormLimits(MultipartBodyLengthLimit = 2_097_152)]
    public async Task<IActionResult> UploadPhoto(Guid id, IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length is 0 or > 2_097_152)
        {
            return Result.Failure<MemberResponse>(GymErrors.InvalidPhoto).ToActionResult();
        }

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return (await Sender.Send(new UploadMemberPhotoCommand(id, file.ContentType, buffer.ToArray()), cancellationToken)).ToActionResult();
    }

    [HttpDelete("members/{id:guid}/profile-photo")]
    [RequiresPermission(PermissionCodes.MemberUpdate)]
    public async Task<IActionResult> RemovePhoto(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new RemoveMemberPhotoCommand(id), cancellationToken)).ToActionResult();
}
