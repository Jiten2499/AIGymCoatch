using LifeFitAI.Api.Contracts;
using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Gym.Members;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
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
    public async Task<IActionResult> Create(CreateMemberRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new CreateMemberCommand(request.Email, request.Password, request.FirstName, request.LastName, request.Phone, request.BranchId, request.PlanId, request.StartDate, request.AutoRenew), cancellationToken)).ToActionResult();

    [HttpGet("members/{id:guid}")]
    [RequiresPermission(PermissionCodes.MemberRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetMemberQuery(id), cancellationToken)).ToActionResult();

    [HttpPut("members/{id:guid}")]
    [RequiresPermission(PermissionCodes.MemberUpdate)]
    public async Task<IActionResult> Update(Guid id, UpdateMemberRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new UpdateMemberCommand(id, request.DateOfBirth, request.Gender, request.EmergencyContactName, request.EmergencyContactPhone, request.Address, request.City, request.State, request.PostalCode, request.Status), cancellationToken)).ToActionResult();
}
