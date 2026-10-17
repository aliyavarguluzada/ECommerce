using ECommerce.Catalog.Application.Categories;
using ECommerce.Catalog.Application.Products;
using ECommerce.Catalog.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Catalog.Api.Common;

public sealed class CatalogExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var status = exception switch
        {
            DomainException => StatusCodes.Status400BadRequest,

            DuplicateCategoryException => StatusCodes.Status409Conflict,

            CategoryInUseException => StatusCodes.Status409Conflict,

            CategoryConcurrencyException => StatusCodes.Status409Conflict,

            ProductConcurrencyException => StatusCodes.Status409Conflict,

            InvalidProductCategoryException => StatusCodes.Status400BadRequest,

            _ => 0

        };

        if (status == 0)
        {
            return false;
        }

        context.Response.StatusCode = status;

        var details = new ProblemDetails
        {
            Status = status,

            Title = status == 409 ? "Catalog conflict" : "Invalid catalog request",

            Detail = exception.Message,

            Instance = context.Request.Path

        };

        if (!await problemDetailsService.TryWriteAsync(
            new ProblemDetailsContext { HttpContext = context, ProblemDetails = details }))
        {
            await context.Response.WriteAsJsonAsync(details, cancellationToken);

        }

        return true;
    }

}
