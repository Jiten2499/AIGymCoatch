using System.Text.RegularExpressions;
using LifeFitAI.SharedKernel.Constants;
using Serilog.Context;

namespace LifeFitAI.Api.Middleware;

public sealed class CorrelationIdMiddleware
{
    public const string ItemKey = "CorrelationId";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderNames.CorrelationId] = correlationId;
        Correlation.Id = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }

        Correlation.Id = null;
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderNames.CorrelationId, out var supplied)
            && Regex.IsMatch(supplied.ToString(), "^[A-Za-z0-9-]{8,64}$"))
        {
            return supplied.ToString();
        }

        return Guid.NewGuid().ToString("D");
    }
}
