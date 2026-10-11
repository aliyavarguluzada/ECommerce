using ECommerce.Inventory.Application;
using ECommerce.Inventory.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace ECommerce.Inventory.Api.Common;

public sealed class ServiceExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, detail) = exception switch
        {
            DomainException domain => (400, domain.Message),
            
            ServiceException error => (error.StatusCode, error.Message),
            DbUpdateConcurrencyException => (409, "Data changed during this request. Reload and retry with the same request ID."),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } =>
                (409, "This request or entity already exists. Retry with the original request ID."),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.CheckViolation } } =>
                (409, "The requested operation violates a stock or order constraint."),
            NpgsqlException => (503, "The service database is unavailable."),
            DbUpdateException { InnerException: NpgsqlException } => (503, "The service database is unavailable."),
            _ => (0, "")
        };
        if (status == 0) return false;
        context.Response.StatusCode = status;
        var problem = new ProblemDetails { Status = status, Title = status switch
            { 400 => "Invalid request", 404 => "Not found", 409 => "Operation conflict", 503 => "Service unavailable", _ => "Request failed" },
            Detail = detail, Instance = context.Request.Path };
        if (!await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem }))
            await context.Response.WriteAsJsonAsync(problem, ct);
        return true;
    }
}
