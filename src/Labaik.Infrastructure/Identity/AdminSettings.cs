namespace Labaik.Infrastructure.Identity;

public sealed class AdminSettings
{
    public const string SectionName = "Admin";

    // Confirmed accounts with these emails are granted the Admin role on sign-in / token refresh.
    // Env vars: Admin__Emails__0, __1, ...
    public string[] Emails { get; init; } = [];
}
