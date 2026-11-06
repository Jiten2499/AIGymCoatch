using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Identity.GetCurrentUser;
using LifeFitAI.Application.Identity.Profile;
using LifeFitAI.Application.Platform;
using LifeFitAI.SharedKernel.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeFitAI.Api.Controllers;

[Authorize]
public sealed class UsersController : ApiControllerBase
{
    public UsersController(MediatR.ISender sender) : base(sender)
    {
    }

    [HttpGet("users/me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken) =>
        (await Sender.Send(new GetCurrentUserQuery(), cancellationToken)).ToActionResult();

    [HttpGet("users/me/access")]
    public async Task<IActionResult> Access(CancellationToken cancellationToken) =>
        (await Sender.Send(new GetMyAccessQuery(), cancellationToken)).ToActionResult();

    [HttpGet("users/me/photo")]
    public Task<IActionResult> MyPhoto(CancellationToken cancellationToken) => Photo(null, cancellationToken);

    [HttpPost("users/me/photo")]
    [RequestSizeLimit(2_097_152)]
    [RequestFormLimits(MultipartBodyLengthLimit = 2_097_152)]
    public async Task<IActionResult> UploadPhoto(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length is 0 or > 2_097_152)
        {
            return Result.Failure<UserProfile>(IdentityErrors.InvalidPhoto).ToActionResult();
        }

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return (await Sender.Send(new UploadMyProfilePhotoCommand(file.ContentType, buffer.ToArray()), cancellationToken)).ToActionResult();
    }

    [HttpDelete("users/me/photo")]
    public async Task<IActionResult> RemovePhoto(CancellationToken cancellationToken) =>
        (await Sender.Send(new RemoveMyProfilePhotoCommand(), cancellationToken)).ToActionResult();

    [HttpGet("users/{id:guid}/photo")]
    public Task<IActionResult> UserPhoto(Guid id, CancellationToken cancellationToken) => Photo(id, cancellationToken);

    private async Task<IActionResult> Photo(Guid? userId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetProfilePhotoQuery(userId), cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        Response.Headers.CacheControl = "private, no-store";
        return File(result.Value.Content, result.Value.ContentType);
    }

    [HttpGet("account/export")]
    public async Task<IActionResult> Export(CancellationToken cancellationToken) =>
        (await Sender.Send(new ExportPersonalDataQuery(), cancellationToken)).ToActionResult();
}
