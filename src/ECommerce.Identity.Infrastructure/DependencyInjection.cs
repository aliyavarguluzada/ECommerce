using ECommerce.Identity.Application;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connection)
    {
        services.AddDbContext<IdentityDbContext>(options => options.UseNpgsql(connection));

        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;

            options.Password.RequiredLength = 12;

            options.Password.RequireDigit = true;

            options.Password.RequireLowercase = true;

            options.Password.RequireUppercase = true;

            options.Password.RequireNonAlphanumeric = true;

            options.Lockout.MaxFailedAccessAttempts = 5;

            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

        }).AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<IdentityDbContext>().AddSignInManager().AddDefaultTokenProviders();

        services.AddScoped<IIdentityService, IdentityService>();

        return services;
    }

    public static async Task SeedRolesAsync(IServiceProvider provider)
    {
        var roles = provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        foreach (var role in new[]
        {
            "Customer",

            "Admin"

        }

        )
        {
            if (!await roles.RoleExistsAsync(role))
            {
                var result = await roles.CreateAsync(new IdentityRole<Guid>(role));

                if (!result.Succeeded)
                {
                    throw new InvalidOperationException("Could not initialize identity roles.");

                }

            }

        }

    }

    public static async Task BootstrapAdminAsync(IServiceProvider provider, string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Set BootstrapAdmin:Email and BootstrapAdmin:Password before bootstrapping.");

        }

        await SeedRolesAsync(provider);

        await using var transaction = await provider.GetRequiredService<IdentityDbContext>().Database.BeginTransactionAsync();

        var users = provider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await users.FindByEmailAsync(email);

        if (user is not null)
        {
            throw new InvalidOperationException("Bootstrap only creates a new account. Use the authenticated admin role API for existing users.");

        }

        user = new ApplicationUser
        {
            Id = Guid.NewGuid(),

            Email = email,

            UserName = email,

            DisplayName = "Administrator"

        };

        var created = await users.CreateAsync(user, password);

        if (!created.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", created.Errors.Select(x => x.Description)));

        }

        foreach (var role in new[]
        {
            "Customer",

            "Admin"

        }

        )
        {
            if (!(await users.AddToRoleAsync(user, role)).Succeeded)
            {
                throw new InvalidOperationException("Could not assign bootstrap administrator roles.");

            }

        }

        await transaction.CommitAsync();

    }

}
