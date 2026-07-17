using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OMS.API.Infrastructure.Exceptions;

namespace OMS.API.Infrastructure.Middlewares;

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var exceptionDetails = GetExceptionDetails(exception);

        if (exceptionDetails.StatusCode == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception occurred while processing the request.");
        }
        else
        {
            logger.LogWarning(
                exception,
                "Handled application exception {ExceptionType} with status code {StatusCode}.",
                exception.GetType().Name,
                exceptionDetails.StatusCode);
        }

        httpContext.Response.StatusCode = exceptionDetails.StatusCode;

        var problemDetails = new ProblemDetails
        {
            Status = exceptionDetails.StatusCode,
            Title = exceptionDetails.Title,
            Detail = exceptionDetails.Detail,
            Type = $"https://httpstatuses.com/{exceptionDetails.StatusCode}"
        };

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception
        });
    }

    private static ExceptionDetails GetExceptionDetails(Exception exception)
    {
        return exception switch
        {
            NotFoundException notFoundException => new(
                StatusCodes.Status404NotFound,
                "Resource not found",
                notFoundException.Message),
            ConflictException conflictException => new(
                StatusCodes.Status409Conflict,
                "Conflict",
                conflictException.Message),
            ForbiddenException forbiddenException => new(
                StatusCodes.Status403Forbidden,
                "Forbidden",
                forbiddenException.Message),
            UnauthorizedException unauthorizedException => new(
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                unauthorizedException.Message),
            BusinessRuleException businessRuleException => new(
                StatusCodes.Status422UnprocessableEntity,
                "Business rule violation",
                businessRuleException.Message),
            ExternalServiceException externalServiceException => new(
                StatusCodes.Status503ServiceUnavailable,
                "External service unavailable",
                externalServiceException.Message),
            _ => new(
                StatusCodes.Status500InternalServerError,
                "Internal server error",
                "An unexpected error occurred.")
        };
    }

    private sealed record ExceptionDetails(
        int StatusCode,
        string Title,
        string Detail);
}
