using Labaik.Application.Common.Authorization;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Common.Provisioning;
using Labaik.Infrastructure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Labaik.Infrastructure.Persistence.Seed;

// Ensures every Admin:Emails address has a confirmed account with the Admin role (idempotent; runs
// on every startup after RoleSeeder). A new account gets a temporary password by email only, through
// the same provisioning flow as company/supervisor accounts; existing confirmed accounts keep theirs.
public static class AdminAccountSeeder
{
    private const string DefaultFullName = "Labaik Admin";

    public static async Task SeedAsync(IServiceProvider services, ILogger logger, CancellationToken cancellationToken = default)
    {
        var emails = services.GetRequiredService<IOptions<AdminSettings>>().Value.Emails
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (emails.Count == 0)
        {
            return;
        }

        var identityService = services.GetRequiredService<IIdentityService>();
        var emailSender = services.GetRequiredService<IEmailSender>();
        var downloadUrl = services.GetRequiredService<IOptions<AppLinksSettings>>().Value.DownloadUrl;

        foreach (var email in emails)
        {
            var account = await identityService.ProvisionAccountAsync(DefaultFullName, email, null, Roles.Admin,
                cancellationToken: cancellationToken);
            if (account.IsFailure)
            {
                logger.LogError("Could not seed admin account {Email}: {Error}", email, account.Error!.Description);
                continue;
            }

            if (account.Value.TemporaryPassword is null)
            {
                logger.LogInformation("Admin account {Email} already exists; Admin role ensured.", email);
                continue;
            }

            var sent = await emailSender.TrySendAsync(email,
                AccountEmails.For(account.Value, "admin", null, downloadUrl), logger, cancellationToken);
            if (sent)
            {
                logger.LogInformation("Seeded admin account {Email}; temporary password emailed.", email);
            }
            else
            {
                logger.LogWarning("Seeded admin account {Email} but the email failed; use forgot-password to set one.", email);
            }
        }
    }
}
