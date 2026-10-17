using ECommerce.Payment.Application;
using ECommerce.Payment.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ECommerce.Payment.Api.Common;

public sealed class ServiceExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var(status, message) = exception switch
        {
            ServiceException error => (error.StatusCode, error.Message),

            DomainException error => (400, error.Message),

            DbUpdateConcurrencyException => (409, "Data changed. Retry the original request."),

            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } => (409, "This account or request already exists."),

            NpgsqlException => (503, "Database is unavailable."),

            DbUpdateException { InnerException: NpgsqlException } => (503, "Database is unavailable."),

            _ => (0, "")};

        if (status == 0)
        {
            return false;
        }

        context.Response.StatusCode = status;

        var problem = new ProblemDetails
        {
            Status = status,

            Title = status switch
            {
                400 => "Invalid request",

                401 => "Authentication failed",

                402 => "Payment declined",

                403 => "Forbidden",

                404 => "Not found",

                409 => "Conflict",

                _ => "Service unavailable"

            },

            Detail = message,

            Instance = context.Request.Path

        };

        if (!await problems.TryWriteAsync(
            new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem }))
        {
            await context.Response.WriteAsJsonAsync(problem, ct);

        }

        return true;
    }

}
