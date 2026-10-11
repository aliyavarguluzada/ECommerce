using ECommerce.Catalog.Application.Categories.Commands.CreateCategory;
using ECommerce.Catalog.Application.Categories.Commands.DeleteCategory;
using ECommerce.Catalog.Application.Categories.Commands.UpdateCategory;
using ECommerce.Catalog.Application.Categories.Queries.GetCategories;
using ECommerce.Catalog.Application.Categories.Queries.GetCategoryById;
using ECommerce.Catalog.Application.Products.Commands.CreateProduct;
using ECommerce.Catalog.Application.Products.Commands.UpdateProduct;
using ECommerce.Catalog.Application.Products.Commands.ChangeProductPrice;
using ECommerce.Catalog.Application.Products.Commands.DiscontinueProduct;
using ECommerce.Catalog.Application.Products.Queries.GetProductById;
using ECommerce.Catalog.Application.Products.Queries.GetProducts;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Catalog.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateCategoryHandler>();
        services.AddScoped<GetCategoryByIdHandler>();
        services.AddScoped<GetCategoriesHandler>();
        services.AddScoped<UpdateCategoryHandler>();
        services.AddScoped<DeleteCategoryHandler>();
        services.AddScoped<CreateProductHandler>();
        services.AddScoped<UpdateProductHandler>();
        services.AddScoped<ChangeProductPriceHandler>();
        services.AddScoped<DiscontinueProductHandler>();
        services.AddScoped<GetProductByIdHandler>();
        services.AddScoped<GetProductsHandler>();
        return services;
    }
}
