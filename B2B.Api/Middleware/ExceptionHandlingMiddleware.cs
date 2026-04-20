using System.Net;
using FluentValidation;
using B2B.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace B2B.Api.Middleware;

public sealed class ExceptionHandlingMiddleware : IMiddleware
{
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddleware(ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment env)
    {
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception ex)
    {
        var traceId = context.TraceIdentifier;

        var (statusCode, title, type, code) = ex switch
        {
            ValidationException => (HttpStatusCode.BadRequest, "Validation failed.", "https://httpstatuses.com/400", "validation_failed"),
            DbUpdateConcurrencyException => (HttpStatusCode.Conflict, "Concurrency conflict.", "https://httpstatuses.com/409", "concurrency_conflict"),
            DbUpdateException => (HttpStatusCode.BadRequest, "Database update failed.", "https://httpstatuses.com/400", "db_update_failed"),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred.", "https://httpstatuses.com/500", "server_error")
        };

        _logger.LogError(ex, "Unhandled exception. TraceId={TraceId}", traceId);

        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = (int)statusCode;
        // We return a B2B.Contracts ApiResponse envelope, not RFC7807 ProblemDetails.
        context.Response.ContentType = "application/json; charset=utf-8";

        IReadOnlyDictionary<string, string[]>? details = null;
        if (ex is ValidationException vex)
        {
            details = vex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());
        }
        else if (_env.IsDevelopment() || _env.IsEnvironment("Testing"))
        {
            details = new Dictionary<string, string[]>
            {
                ["exceptionType"] = [ex.GetType().FullName ?? ex.GetType().Name],
                ["message"] = [ex.Message]
            };
        }

        var payload = ApiResponse<object>.Fail(
            new ApiError(code, title, details),
            traceId
        );

        await context.Response.WriteAsJsonAsync(payload);
    }
}

