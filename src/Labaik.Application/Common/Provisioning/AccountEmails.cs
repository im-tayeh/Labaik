using Labaik.Application.Common.Interfaces;
using Labaik.Application.Common.Models;
using Microsoft.Extensions.Logging;

namespace Labaik.Application.Common.Provisioning;

// Emails for accounts created top-down (admin -> company -> supervisor / pilgrim).
public static class AccountEmails
{
    // New account: login email + temporary password. Existing account: a notice without credentials.
    public static (string Subject, string Body) For(
        ProvisionedAccount account, string accountKind, string? companyName, string? downloadUrl = null)
    {
        var at = companyName is null ? string.Empty : $" for {companyName}";

        if (account.TemporaryPassword is { } password)
        {
            return ($"Your Labaik {accountKind} account",
                $"""
                Hello {account.FullName},

                {companyName ?? "Labaik"} added you as a {accountKind}.

                Sign in with:
                  Email: {account.Email}
                  Temporary password: {password}

                You will be asked to choose a new password on first sign-in.
                {(string.IsNullOrWhiteSpace(downloadUrl) ? string.Empty : $"Download the app: {downloadUrl}")}
                """);
        }

        return ($"You now have Labaik {accountKind} access",
            $"""
            Hello {account.FullName},

            Your existing Labaik account ({account.Email}) now has {accountKind} access{at}.
            Sign in with your current password. If you were already signed in, sign in again to see the change.
            """);
    }

    // Email delivery must not undo a committed provisioning: failures are logged, not thrown.
    public static async Task<bool> TrySendAsync(
        this IEmailSender emailSender,
        string to,
        (string Subject, string Body) email,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            await emailSender.SendAsync(to, email.Subject, email.Body, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not send the account email to {Email}.", to);
            return false;
        }
    }
}
