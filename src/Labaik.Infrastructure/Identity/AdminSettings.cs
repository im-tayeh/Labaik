namespace Labaik.Infrastructure.Identity;

public sealed class AdminSettings
{
    public const string SectionName = "Admin";

    // AdminAccountSeeder ensures a confirmed Admin account for each email at startup (new accounts get
    // a temporary password by email); confirmed accounts are also granted Admin on sign-in / token refresh.
    // Env vars: Admin__Emails__0, __1, ...
    public string[] Emails { get; init; } = [];
}
