using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Chat;
using LifeFitAI.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeFitAI.Api.Controllers;

[Authorize]
public sealed class ChatController : ApiControllerBase
{
    public ChatController(MediatR.ISender sender) : base(sender)
    {
    }

    [HttpGet("chat/conversations")]
    [RequiresPermission(PermissionCodes.ChatRead)]
    public async Task<IActionResult> Inbox(DateTime? before, int? take, string? term, string? filter, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListChatInboxQuery(before, take ?? 30, term, filter), cancellationToken)).ToActionResult();

    [HttpPost("chat/conversations")]
    [RequiresPermission(PermissionCodes.ChatSend)]
    public async Task<IActionResult> Open(OpenChatRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new OpenDirectChatCommand(request.RecipientUserId), cancellationToken)).ToActionResult();

    [HttpGet("chat/conversations/{id:guid}/messages")]
    [RequiresPermission(PermissionCodes.ChatRead)]
    public async Task<IActionResult> Messages(Guid id, DateTime? before, Guid? beforeId, int? take, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListChatMessagesQuery(id, before, beforeId, take ?? 30), cancellationToken)).ToActionResult();

    [HttpPost("chat/conversations/{id:guid}/messages")]
    [RequiresPermission(PermissionCodes.ChatSend)]
    public async Task<IActionResult> Send(Guid id, SendChatRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new SendChatMessageCommand(id, request.Body, request.ClientMessageId), cancellationToken)).ToActionResult();

    [HttpPost("chat/conversations/{id:guid}/attachments")]
    [RequiresPermission(PermissionCodes.ChatSend)]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Attach(Guid id, IFormFile file, string? clientMessageId, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return (await Sender.Send(new SendChatAttachmentCommand(id, file.FileName, file.ContentType, buffer.ToArray(), clientMessageId), cancellationToken)).ToActionResult();
    }

    [HttpPost("chat/conversations/{id:guid}/read")]
    [RequiresPermission(PermissionCodes.ChatRead)]
    public async Task<IActionResult> Read(Guid id, ReadChatRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new MarkChatReadCommand(id, request.MessageId), cancellationToken)).ToActionResult();

    [HttpGet("chat/people/{userId:guid}/photo")]
    [RequiresPermission(PermissionCodes.ChatRead)]
    public async Task<IActionResult> Photo(Guid userId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetChatPhotoQuery(userId), cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        Response.Headers.CacheControl = "private, no-store";
        return File(result.Value.Content, result.Value.ContentType);
    }

    [HttpGet("chat/recipients")]
    [RequiresPermission(PermissionCodes.ChatSend)]
    public async Task<IActionResult> Recipients(string? term, CancellationToken cancellationToken) =>
        (await Sender.Send(new SearchChatRecipientsQuery(term), cancellationToken)).ToActionResult();

    [HttpGet("chat/summary")]
    [RequiresPermission(PermissionCodes.ChatRead)]
    public async Task<IActionResult> Summary(CancellationToken cancellationToken) =>
        (await Sender.Send(new GetChatSummaryQuery(), cancellationToken)).ToActionResult();

    [HttpGet("chat/permissions")]
    [RequiresPermission(PermissionCodes.ChatConfigure)]
    public async Task<IActionResult> Permissions(CancellationToken cancellationToken) =>
        (await Sender.Send(new GetChatPermissionsQuery(), cancellationToken)).ToActionResult();

    [HttpPut("chat/permissions")]
    [RequiresPermission(PermissionCodes.ChatConfigure)]
    public async Task<IActionResult> SavePermissions(UpdateChatPermissionsRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new UpdateChatPermissionsCommand(request.Rules), cancellationToken)).ToActionResult();

    [HttpGet("chat/attachments/{id:guid}")]
    [RequiresPermission(PermissionCodes.ChatRead)]
    public async Task<IActionResult> Attachment(Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new DownloadChatAttachmentQuery(id), cancellationToken).ConfigureAwait(false);
        return result.IsFailure ? result.ToActionResult() : File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }
}

public sealed record OpenChatRequest(Guid RecipientUserId);

public sealed record SendChatRequest(string Body, string? ClientMessageId);

public sealed record ReadChatRequest(Guid MessageId);

public sealed record UpdateChatPermissionsRequest(IReadOnlyList<ChatRuleResponse> Rules);
