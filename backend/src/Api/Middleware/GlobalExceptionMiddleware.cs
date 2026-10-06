using FluentValidation;
using LifeFitAI.Application.Common.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Api.Middleware;

public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            await WriteProblemAsync(context, exception);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, Exception exception)
    {
        var problem = CreateProblem(context, exception);
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;

        await context.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: context.RequestAborted);
    }

    private ProblemDetails CreateProblem(HttpContext context, Exception exception)
    {
        if (exception is DbUpdateConcurrencyException)
        {
            _logger.LogWarning("Concurrency conflict {ExceptionType}", exception.GetType().Name);
            return CreateProblemDetails(context, StatusCodes.Status409Conflict, "Conflict", "The record was changed by another request. Reload and try again.");
        }

        if (exception is DbUpdateException update && IsUniqueConflict(update))
        {
            _logger.LogWarning("Unique constraint conflict {ExceptionType}", update.GetType().Name);
            return CreateProblemDetails(context, StatusCodes.Status409Conflict, "Conflict", "The record conflicts with an existing record.");
        }

        if (exception is DbUpdateException)
        {
            _logger.LogError("Database update failed {ExceptionType}", exception.GetType().Name);
            return CreateProblemDetails(context, StatusCodes.Status500InternalServerError, "Internal Server Error", "An unexpected error occurred.");
        }

        if (exception is ValidationException validationException)
        {
            _logger.LogWarning(validationException, "Request validation failed");
            return CreateValidationProblem(context, validationException);
        }

        if (exception is AppException appException)
        {
            _logger.LogWarning(appException, "Request failed with {StatusCode}", appException.StatusCode);
            return CreateProblemDetails(context, appException.StatusCode, appException.Title, appException.Message);
        }

        _logger.LogError("Unhandled exception {ExceptionType}", exception.GetType().Name);
        var detail = _environment.IsDevelopment()
            ? exception.Message
            : "An unexpected error occurred.";

        return CreateProblemDetails(
            context,
            StatusCodes.Status500InternalServerError,
            "Internal Server Error",
            detail);
    }

    private static ValidationProblemDetails CreateValidationProblem(
        HttpContext context,
        ValidationException exception)
    {
        var errors = exception.Errors
            .GroupBy(error => string.IsNullOrWhiteSpace(error.PropertyName) ? "request" : error.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).ToArray());

        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred.",
            Instance = context.Request.Path,
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
        };

        AddCorrelation(context, problem);
        return problem;
    }

    private static ProblemDetails CreateProblemDetails(
        HttpContext context,
        int statusCode,
        string title,
        string detail)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path,
            Type = $"https://httpstatuses.com/{statusCode}",
        };

        AddCorrelation(context, problem);
        return problem;
    }

    private static bool IsUniqueConflict(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current.GetType().Name == "SqlException"
                && (current.Message.Contains("unique", StringComparison.OrdinalIgnoreCase)
                    || current.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddCorrelation(HttpContext context, ProblemDetails problem)
    {
        problem.Extensions["traceId"] = context.TraceIdentifier;

        if (context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var value)
            && value is string correlationId)
        {
            problem.Extensions["correlationId"] = correlationId;
        }
    }
}
