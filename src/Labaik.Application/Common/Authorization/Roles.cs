namespace Labaik.Application.Common.Authorization;

// Global roles. A pilgrim is any authenticated user without a role (the default).
public static class Roles
{
    public const string Admin = "Admin";
    public const string Supervisor = "Supervisor";
    public const string Company = "Company"; // owner account of a company (created by an admin)

    public static readonly IReadOnlyList<string> All = [Admin, Supervisor, Company];
}
