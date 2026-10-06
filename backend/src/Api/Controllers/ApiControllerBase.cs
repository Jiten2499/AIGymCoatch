using Asp.Versioning;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Identity;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LifeFitAI.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}")]
public abstract class ApiControllerBase : ControllerBase
{
    protected ISender Sender { get; }

    protected ApiControllerBase(ISender sender) => Sender = sender;
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequiresPermissionAttribute : Attribute, IAsyncActionFilter
{
    private readonly string _permissionCode;

    public RequiresPermissionAttribute(string permissionCode) => _permissionCode = permissionCode;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var actor = context.HttpContext.RequestServices.GetRequiredService<ICurrentActor>();
        if (actor.UserId is not Guid userId)
        {
            context.Result = new Endpoints.HttpResultAction(Results.Unauthorized());
            return;
        }

        Guid tenantId;
        if (context.RouteData.Values.TryGetValue("tenantId", out var routeValue)
            && Guid.TryParse(routeValue?.ToString(), out var routeTenantId))
        {
            tenantId = routeTenantId;
        }
        else if (actor.TenantId is Guid currentTenantId)
        {
            tenantId = currentTenantId;
        }
        else
        {
            context.Result = new Endpoints.HttpResultAction(Results.Unauthorized());
            return;
        }

        var permissions = context.HttpContext.RequestServices.GetRequiredService<IPermissionService>();
        if (!await permissions.HasPermissionAsync(userId, tenantId, _permissionCode, context.HttpContext.RequestAborted).ConfigureAwait(false))
        {
            context.Result = new Endpoints.HttpResultAction(Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: "You do not have permission to perform this action."));
            return;
        }

        await next().ConfigureAwait(false);
    }
}
