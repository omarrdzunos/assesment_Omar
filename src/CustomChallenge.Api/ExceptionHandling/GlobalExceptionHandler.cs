using CustomChallenge.Api.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CustomChallenge.Api.ExceptionHandling;

// Modern .NET 8+ replacement for a custom exception-handling middleware:
// a plain DI class, trivially unit-testable, and composable with
// AddProblemDetails() for a consistent RFC 7807 error shape app-wide.
public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            ProductNotFoundException or OrderNotFoundException
                => (StatusCodes.Status404NotFound, "Resource not found."),
            ProductInactiveException or DuplicateOrderItemsException or ArgumentException
                => (StatusCodes.Status400BadRequest, "Invalid request."),
            InsufficientStockException or OrderNotPendingException
                => (StatusCodes.Status409Conflict, "Business rule conflict."),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception processing {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = statusCode == StatusCodes.Status500InternalServerError
                ? "An unexpected error occurred."
                : exception.Message
        }, cancellationToken);

        return true;
    }
}
