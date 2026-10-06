using LifeFitAI.SharedKernel.Results;
using Microsoft.AspNetCore.Mvc;

namespace LifeFitAI.Api.Endpoints;

public static class ResultHttp
{
    public static IActionResult ToActionResult(this Result result) => new HttpResultAction(ToHttpResult(result));

    public static IActionResult ToActionResult<T>(this Result<T> result) => new HttpResultAction(ToHttpResult(result));

    public static IResult ToHttpResult(this Result result) =>
        result.IsSuccess ? Results.NoContent() : Problem(result.Error);

    public static IResult ToHttpResult<T>(this Result<T> result) =>
        result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);

    private static IResult Problem(Error error)
    {
        var status = error.Code switch
        {
            "identity.invalid_credentials" or "identity.refresh_invalid" or "identity.user_disabled" => StatusCodes.Status401Unauthorized,
            "authorization.forbidden" or "tenant.not_member" or "tenant.unavailable" or "tenant.not_business" or "role.super_admin_rejected" or "membership.owner_removal" or "gym.feature_disabled" => StatusCodes.Status403Forbidden,
            "ai.unavailable" => StatusCodes.Status503ServiceUnavailable,
            "ai.quota" => StatusCodes.Status429TooManyRequests,
            _ when error.Code.EndsWith("conflict", StringComparison.Ordinal) => StatusCodes.Status409Conflict,
            _ when error.Code.EndsWith("not_found", StringComparison.Ordinal) => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status400BadRequest,
        };

        return Results.Problem(statusCode: status, title: Title(status), detail: error.Description);
    }

    private static string Title(int status) => status switch
    {
        StatusCodes.Status401Unauthorized => "Unauthorized",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status404NotFound => "Not Found",
        StatusCodes.Status409Conflict => "Conflict",
        StatusCodes.Status429TooManyRequests => "Too Many Requests",
        StatusCodes.Status503ServiceUnavailable => "Service Unavailable",
        _ => "Bad Request",
    };
}

public sealed class HttpResultAction : IActionResult, IStatusCodeHttpResult
{
    private readonly IResult _result;

    public HttpResultAction(IResult result) => _result = result;

    public int? StatusCode => _result is IStatusCodeHttpResult status ? status.StatusCode : null;

    public Task ExecuteResultAsync(ActionContext context) => _result.ExecuteAsync(context.HttpContext);
}
