using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Licensing;
using LifeFitAI.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeFitAI.Api.Controllers;

[Authorize]
public sealed class LicensesController : ApiControllerBase
{
    public LicensesController(MediatR.ISender sender) : base(sender)
    {
    }

    [HttpGet("licenses/current")]
    [RequiresPermission(PermissionCodes.LicenseRead)]
    public async Task<IActionResult> Current(CancellationToken cancellationToken) =>
        (await Sender.Send(new GetCurrentLicenseQuery(), cancellationToken)).ToActionResult();

    [HttpGet("features")]
    [RequiresPermission(PermissionCodes.FeatureRead)]
    public async Task<IActionResult> Features(CancellationToken cancellationToken) =>
        (await Sender.Send(new GetFeaturesQuery(), cancellationToken)).ToActionResult();
}
