using Labaik.Application.Common.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Labaik.Infrastructure.Persistence.Seed;

// Ensures every application role exists (idempotent; runs on every startup).
public static class RoleSeeder
{
    public static async Task SeedAsync(IServiceProvider services, ILogger logger)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        foreach (var role in Roles.All)
        {
            if (await roleManager.RoleExistsAsync(role))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(new IdentityRole<Guid>(role) { Id = Guid.NewGuid() });
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not create role '{role}': {string.Join(" ", result.Errors.Select(e => e.Description))}");
            }

            logger.LogInformation("Created role {Role}.", role);
        }
    }
}
