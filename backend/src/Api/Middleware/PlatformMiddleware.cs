using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Infrastructure.Platform;
using Microsoft.Extensions.Options;
using Serilog.Context;

namespace LifeFitAI.Api.Middleware;

public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IHostEnvironment _environment;

    public SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment environment)
    {
        _next = next;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers["Content-Security-Policy"] = SwaggerUiRequest(context)
            ? SwaggerContentSecurityPolicy
            : ClosedContentSecurityPolicy;
        if (_environment.IsProduction())
        {
            context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        }

        await _next(context);
    }

    private bool SwaggerUiRequest(HttpContext context) =>
        _environment.IsDevelopment() && context.Request.Path.StartsWithSegments("/swagger");

    private const string ClosedContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";

    private const string SwaggerContentSecurityPolicy =
        "default-src 'none'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self' data:; connect-src 'self'; frame-ancestors 'none'";
}

public sealed class MaintenanceMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SecurityOptions _security;

    public MaintenanceMiddleware(RequestDelegate next, IOptions<SecurityOptions> security)
    {
        _next = next;
        _security = security.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (_security.MaintenanceMode && !context.Request.Path.StartsWithSegments("/health"))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new { title = "Service Unavailable", status = 503, detail = "The service is temporarily unavailable." });
            return;
        }

        await _next(context);
    }
}

public sealed class TenantStatusMiddleware
{
    private readonly RequestDelegate _next;

    public TenantStatusMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ITenantRepository tenants, ICacheService cache)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var allowed = path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/v1/auth", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/v1/tenants", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/v1/platform", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase);
        if (!allowed && context.User.FindFirst("tenant_id")?.Value is string tenantValue && Guid.TryParse(tenantValue, out var tenantId))
        {
            var key = TenantCache.Status(tenantId);
            var status = await cache.GetAsync<string>(key, context.RequestAborted);
            if (status is null)
            {
                var tenant = await tenants.GetAsync(tenantId, context.RequestAborted);
                status = tenant?.Status.ToString() ?? "Missing";
                await cache.SetAsync(key, status, TimeSpan.FromSeconds(30), context.RequestAborted);
            }

            if (status == TenantStatus.Suspended.ToString())
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { title = "Forbidden", status = 403, detail = "This workspace is suspended." });
                return;
            }
        }

        var userId = context.User.FindFirst("sub")?.Value;
        var tenantClaim = context.User.FindFirst("tenant_id")?.Value;
        using (LogContext.PushProperty("UserId", userId))
        using (LogContext.PushProperty("TenantId", tenantClaim))
        {
            await _next(context);
        }
    }
}

public sealed class IdempotencyFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue("Idempotency-Key", out var supplied))
        {
            await next();
            return;
        }

        var key = supplied.ToString().Trim();
        if (key.Length is < 8 or > 100)
        {
            context.Result = new HttpResultAction(Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Bad Request", detail: "Idempotency-Key must be 8 to 100 characters."));
            return;
        }

        var actor = context.HttpContext.RequestServices.GetRequiredService<ICurrentActor>();
        if (actor.UserId is not Guid userId || actor.TenantId is not Guid tenantId)
        {
            await next();
            return;
        }

        var store = context.HttpContext.RequestServices.GetRequiredService<IIdempotencyStore>();
        var operation = context.HttpContext.Request.Path.Value ?? "request";
        if (await store.ExistsAsync(tenantId, userId, operation, key, context.HttpContext.RequestAborted))
        {
            context.Result = new HttpResultAction(Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: "This request was already accepted."));
            return;
        }

        var executed = await next();
        if (executed.Result is IStatusCodeHttpResult status && status.StatusCode is >= 200 and < 300)
        {
            await store.RememberAsync(tenantId, userId, operation, key, status.StatusCode ?? 200, context.HttpContext.RequestAborted);
            var unitOfWork = context.HttpContext.RequestServices.GetRequiredService<IUnitOfWork>();
            await unitOfWork.SaveChangesAsync(context.HttpContext.RequestAborted);
        }
    }
}
