using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace ECommerce.Security;

public static class OpenApiExtensions
{
    public static IServiceCollection AddCommerceOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(options => options.AddDocumentTransformer((document, context, cancellationToken) =>
        {
            document.Components ??= new OpenApiComponents();

            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

            document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,

                Scheme = "bearer",

                BearerFormat = "JWT",

                Description = "Sign in at POST /api/identity/login and paste accessToken here (without the Bearer prefix)."

            };

            document.Components.SecuritySchemes["ServiceKey"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,

                In = ParameterLocation.Header,

                Name = "X-Service-Key",

                Description = "Internal service credential. Customer tokens cannot call internal endpoints."

            };

            foreach (var description in context.DescriptionGroups.SelectMany(group => group.Items))
            {
                var metadata = description.ActionDescriptor.EndpointMetadata;

                var authorization = metadata.OfType<IAuthorizeData>().ToArray();

                if (authorization.Length == 0 || metadata.OfType<IAllowAnonymous>().Any())
                {
                    continue;

                }

                var path = "/" + description.RelativePath?.Split('?')[0];

                if (!document.Paths.TryGetValue(path, out var pathItem) || pathItem.Operations is null)
                {
                    continue;

                }

                var operation = pathItem.Operations.FirstOrDefault(
                    pair => string.Equals(
                        pair.Key.ToString(),
                        description.HttpMethod,
                        StringComparison.OrdinalIgnoreCase)).Value;

                if (operation is null)
                {
                    continue;

                }

                var internalOnly = authorization.Any(
                    data => data.Policy is "OrderService" or "CartService" or "PaymentService" or "OrderReaders");

                operation.Security = [new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(internalOnly ? "ServiceKey" : "Bearer", document)] = []

                }

                ];

            }

            return Task.CompletedTask;
        }));

        return services;
    }

}
