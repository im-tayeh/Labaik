using Labaik.Application.Common.Authorization;

namespace Labaik.Api.Extensions;

public static class AuthorizationPolicies
{
    public const string Admin = "Admin";
    public const string Supervisor = "Supervisor"; // group supervisors only
    public const string Company = "Company";       // company owner accounts

    public static IServiceCollection AddAppAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(Admin, p => p.RequireRole(Roles.Admin))
            .AddPolicy(Supervisor, p => p.RequireRole(Roles.Supervisor))
            .AddPolicy(Company, p => p.RequireRole(Roles.Company));

        return services;
    }
}
