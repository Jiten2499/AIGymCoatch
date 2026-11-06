using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Storage;
using LifeFitAI.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeFitAI.Api.Controllers;

[Authorize]
public sealed class IntegrationsController : ApiControllerBase
{
    private readonly IConfiguration _configuration;

    public IntegrationsController(MediatR.ISender sender, IConfiguration configuration) : base(sender) =>
        _configuration = configuration;

    [HttpGet("integrations/google-drive")]
    [RequiresPermission(PermissionCodes.IntegrationRead)]
    public async Task<IActionResult> GetGoogleDrive(CancellationToken cancellationToken) =>
        (await Sender.Send(new GetGoogleDriveConnectionQuery(), cancellationToken)).ToActionResult();

    [HttpPost("integrations/google-drive/connect")]
    [RequiresPermission(PermissionCodes.IntegrationManage)]
    public async Task<IActionResult> ConnectGoogleDrive(ConnectGoogleDriveRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new BeginGoogleDriveConnectionCommand(request.ClientKind, request.ReturnPath), cancellationToken)).ToActionResult();

    [HttpPost("integrations/google-drive/test")]
    [RequiresPermission(PermissionCodes.IntegrationManage)]
    public async Task<IActionResult> TestGoogleDrive(CancellationToken cancellationToken) =>
        (await Sender.Send(new TestGoogleDriveConnectionCommand(), cancellationToken)).ToActionResult();

    [HttpPost("integrations/google-drive/disconnect")]
    [RequiresPermission(PermissionCodes.IntegrationManage)]
    public async Task<IActionResult> DisconnectGoogleDrive(CancellationToken cancellationToken) =>
        (await Sender.Send(new DisconnectGoogleDriveCommand(), cancellationToken)).ToActionResult();

    [HttpPut("integrations/google-drive/progress-photos")]
    [RequiresPermission(PermissionCodes.IntegrationManage)]
    public async Task<IActionResult> ProgressPhotos(UpdateProgressPhotoStorageRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new UpdateProgressPhotoStorageCommand(request.Enabled), cancellationToken)).ToActionResult();

    [HttpGet("integrations/google-drive/folders")]
    [RequiresPermission(PermissionCodes.IntegrationManage)]
    public async Task<IActionResult> Folders(CancellationToken cancellationToken) =>
        (await Sender.Send(new ListGoogleDriveFoldersQuery(), cancellationToken)).ToActionResult();

    [HttpPost("integrations/google-drive/folder")]
    [RequiresPermission(PermissionCodes.IntegrationManage)]
    public async Task<IActionResult> Folder(ChangeGoogleDriveFolderRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new ChangeGoogleDriveFolderCommand(request.FolderId), cancellationToken)).ToActionResult();

    [AllowAnonymous]
    [HttpGet("integrations/google-drive/callback")]
    public async Task<IActionResult> GoogleDriveCallback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new CompleteGoogleDriveOAuthCommand(code, state, error), cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            return Redirect(ToAbsolute(result.Value));
        }

        var fallback = _configuration["GoogleDrive:WebSuccessRedirectUri"] ?? "/business/integrations";
        var separator = fallback.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return Redirect(ToAbsolute($"{fallback}{separator}status=failed&error={Uri.EscapeDataString(result.Error.Code)}"));
    }

    [HttpPost("storage/files")]
    [RequiresPermission(PermissionCodes.StorageFileWrite)]
    [RequestSizeLimit(StorageKeys.MaxFileBytes + 1024)]
    public async Task<IActionResult> UploadFile(IFormFile file, [FromForm] string logicalKey, [FromForm] string? entityType, [FromForm] Guid? entityId, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
        var result = await Sender.Send(
            new UploadStoredFileCommand(logicalKey, file.FileName, file.ContentType, memory.ToArray(), entityType, entityId),
            cancellationToken).ConfigureAwait(false);
        return result.ToActionResult();
    }

    [HttpGet("storage/files/{id:guid}")]
    [RequiresPermission(PermissionCodes.StorageFileRead)]
    public async Task<IActionResult> GetFile(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetStoredFileQuery(id), cancellationToken)).ToActionResult();

    [HttpGet("storage/files/{id:guid}/content")]
    [RequiresPermission(PermissionCodes.StorageFileRead)]
    public async Task<IActionResult> DownloadFile(Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new DownloadStoredFileQuery(id), cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }

    [HttpDelete("storage/files/{id:guid}")]
    [RequiresPermission(PermissionCodes.StorageFileWrite)]
    public async Task<IActionResult> DeleteFile(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new DeleteStoredFileCommand(id), cancellationToken)).ToActionResult();

    private string ToAbsolute(string target)
    {
        if (Uri.TryCreate(target, UriKind.Absolute, out _))
        {
            return target;
        }

        var publicBase = _configuration["GoogleDrive:PublicApiBaseUrl"];
        if (!string.IsNullOrWhiteSpace(publicBase) && target.StartsWith('/'))
        {
            return publicBase.TrimEnd('/') + target;
        }

        if (target.StartsWith('/'))
        {
            var webBase = _configuration["GoogleDrive:WebAppBaseUrl"];
            if (!string.IsNullOrWhiteSpace(webBase))
            {
                return webBase.TrimEnd('/') + target;
            }
        }

        return target;
    }
}

public sealed record ConnectGoogleDriveRequest(string ClientKind, string? ReturnPath);

public sealed record UpdateProgressPhotoStorageRequest(bool Enabled);

public sealed record ChangeGoogleDriveFolderRequest(string FolderId);
