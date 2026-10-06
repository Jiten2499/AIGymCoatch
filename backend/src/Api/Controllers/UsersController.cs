using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Identity.GetCurrentUser;
using LifeFitAI.Application.Platform;
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

    [HttpGet("account/export")]
    public async Task<IActionResult> Export(CancellationToken cancellationToken) =>
        (await Sender.Send(new ExportPersonalDataQuery(), cancellationToken)).ToActionResult();
}
