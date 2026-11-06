using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Communications;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeFitAI.Api.Controllers;

[Authorize]
public sealed class CommunicationsController : ApiControllerBase
{
    public CommunicationsController(MediatR.ISender sender) : base(sender)
    {
    }

    [HttpGet("communications")]
    [RequiresPermission(PermissionCodes.CommunicationRead)]
    public async Task<IActionResult> List(string? term, CommunicationType? type, CommunicationStatus? status, CommunicationAudienceType? audienceType, DateTime? publishFrom, DateTime? publishTo, int? page, int? pageSize, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListCommunicationsQuery(term, type, status, page ?? 1, pageSize ?? 20, audienceType, publishFrom, publishTo), cancellationToken)).ToActionResult();

    [HttpGet("communications/{id:guid}")]
    [RequiresPermission(PermissionCodes.CommunicationRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetCommunicationQuery(id), cancellationToken)).ToActionResult();

    [HttpPost("communications")]
    [RequiresPermission(PermissionCodes.CommunicationCreate)]
    public async Task<IActionResult> Create(CommunicationRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new CreateCommunicationCommand(
            request.Type,
            request.Title,
            request.Message,
            request.Priority,
            request.AudienceType,
            request.AudiencePackageId,
            request.AudienceTrainerId,
            request.ExpiringWithinDays,
            request.SelectedMemberIds,
            request.PublishAt,
            request.ExpiresAt,
            request.PublishNow,
            request.SelectedTrainerIds,
            request.SelectedStaffIds), cancellationToken)).ToActionResult();

    [HttpPut("communications/{id:guid}")]
    [RequiresPermission(PermissionCodes.CommunicationUpdate)]
    public async Task<IActionResult> Update(Guid id, CommunicationRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new UpdateCommunicationCommand(
            id,
            request.Type,
            request.Title,
            request.Message,
            request.Priority,
            request.AudienceType,
            request.AudiencePackageId,
            request.AudienceTrainerId,
            request.ExpiringWithinDays,
            request.SelectedMemberIds,
            request.PublishAt,
            request.ExpiresAt,
            request.SelectedTrainerIds,
            request.SelectedStaffIds,
            request.PublishNow), cancellationToken)).ToActionResult();

    [HttpPost("communications/{id:guid}/image")]
    [RequiresPermission(PermissionCodes.CommunicationUpdate)]
    [RequestSizeLimit(2_097_152)]
    [RequestFormLimits(MultipartBodyLengthLimit = 2_097_152)]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length is 0 or > 2_097_152)
        {
            return Result.Failure<CommunicationDetails>(GymErrors.InvalidPhoto).ToActionResult();
        }

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return (await Sender.Send(new AttachCommunicationImageCommand(id, file.ContentType, buffer.ToArray()), cancellationToken)).ToActionResult();
    }

    [HttpGet("communications/{id:guid}/image")]
    [RequiresPermission(PermissionCodes.CommunicationRead)]
    public async Task<IActionResult> Image(Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetCommunicationImageQuery(id), cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        Response.Headers.CacheControl = "private, no-store";
        return File(result.Value.Content, result.Value.ContentType);
    }

    [HttpPost("communications/{id:guid}/publish")]
    [RequiresPermission(PermissionCodes.CommunicationPublish)]
    public async Task<IActionResult> Publish(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new PublishCommunicationCommand(id), cancellationToken)).ToActionResult();

    [HttpPost("communications/{id:guid}/archive")]
    [RequiresPermission(PermissionCodes.CommunicationArchive)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new ArchiveCommunicationCommand(id), cancellationToken)).ToActionResult();

    [HttpGet("member/communications")]
    [RequiresPermission(PermissionCodes.CommunicationRead)]
    public async Task<IActionResult> MemberList(CommunicationType? type, int? page, int? pageSize, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListMemberCommunicationsQuery(type, page ?? 1, pageSize ?? 20), cancellationToken)).ToActionResult();

    [HttpGet("member/communications/{id:guid}")]
    [RequiresPermission(PermissionCodes.CommunicationRead)]
    public async Task<IActionResult> MemberGet(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetMemberCommunicationQuery(id), cancellationToken)).ToActionResult();

    [HttpPost("communications/audience-preview")]
    [RequiresPermission(PermissionCodes.CommunicationCreate)]
    public async Task<IActionResult> Preview(CommunicationRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new PreviewCommunicationAudienceQuery(request.AudienceType, request.AudiencePackageId, request.AudienceTrainerId, request.ExpiringWithinDays, request.SelectedMemberIds, request.SelectedTrainerIds, request.SelectedStaffIds), cancellationToken)).ToActionResult();

    [HttpGet("communications/inbox")]
    [RequiresPermission(PermissionCodes.CommunicationRead)]
    public async Task<IActionResult> Inbox(CommunicationType? type, bool? unreadOnly, int? page, int? pageSize, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListInboxCommunicationsQuery(type, unreadOnly ?? false, page ?? 1, pageSize ?? 20), cancellationToken)).ToActionResult();

    [HttpGet("communications/inbox/unread-count")]
    [RequiresPermission(PermissionCodes.CommunicationRead)]
    public async Task<IActionResult> UnreadCount(CancellationToken cancellationToken) =>
        (await Sender.Send(new CountUnreadCommunicationsQuery(), cancellationToken)).ToActionResult();

    [HttpGet("communications/inbox/summary")]
    [RequiresPermission(PermissionCodes.CommunicationRead)]
    public async Task<IActionResult> InboxSummary(CancellationToken cancellationToken) =>
        (await Sender.Send(new GetCommunicationInboxSummaryQuery(), cancellationToken)).ToActionResult();

    [HttpGet("communications/inbox/sync")]
    [RequiresPermission(PermissionCodes.CommunicationRead)]
    public async Task<IActionResult> InboxSync(DateTime? sinceUtc, int? take, CancellationToken cancellationToken) =>
        (await Sender.Send(new SyncInboxCommunicationsQuery(sinceUtc, take ?? 30), cancellationToken)).ToActionResult();

    [HttpGet("communications/inbox/{id:guid}")]
    [RequiresPermission(PermissionCodes.CommunicationRead)]
    public async Task<IActionResult> InboxItem(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetInboxCommunicationQuery(id), cancellationToken)).ToActionResult();

    [HttpPost("communications/inbox/{id:guid}/read")]
    [RequiresPermission(PermissionCodes.CommunicationRead)]
    public async Task<IActionResult> InboxRead(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new MarkInboxCommunicationReadCommand(id), cancellationToken)).ToActionResult();

    [HttpPost("communications/inbox/read-all")]
    [RequiresPermission(PermissionCodes.CommunicationRead)]
    public async Task<IActionResult> InboxReadAll(CancellationToken cancellationToken) =>
        (await Sender.Send(new MarkAllInboxCommunicationsReadCommand(), cancellationToken)).ToActionResult();

    [HttpPost("member/communications/{id:guid}/read")]
    [RequiresPermission(PermissionCodes.CommunicationRead)]
    public async Task<IActionResult> MemberRead(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new MarkCommunicationReadCommand(id), cancellationToken)).ToActionResult();
}

public sealed record CommunicationRequest(
    CommunicationType Type,
    string Title,
    string Message,
    CommunicationPriority Priority,
    CommunicationAudienceType AudienceType,
    Guid? AudiencePackageId,
    Guid? AudienceTrainerId,
    int? ExpiringWithinDays,
    IReadOnlyList<Guid>? SelectedMemberIds,
    DateTime? PublishAt,
    DateTime? ExpiresAt,
    bool PublishNow = false,
    IReadOnlyList<Guid>? SelectedTrainerIds = null,
    IReadOnlyList<Guid>? SelectedStaffIds = null);
