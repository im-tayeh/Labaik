using Labaik.Application.Common.Interfaces;
using Labaik.Application.Common.Models;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Identity;
using Labaik.Domain.Companies;
using Labaik.Domain.Companies.Enums;
using Labaik.Application.Common.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace Labaik.Infrastructure.Identity;

internal sealed class IdentityService(
    UserManager<ApplicationUser> userManager,
    IVerificationCodeService verificationCodeService,
    IRefreshTokenService refreshTokenService,
    IOptions<AdminSettings> adminOptions,
    ILogger<IdentityService> logger,
    IAppDbContext context,
    TimeProvider timeProvider,
    IAccountAccessCache accessCache,
    IRealtimeNotifier notifier) : IIdentityService
{
    public async Task<Result<Guid>> CreateUserAsync(string fullName, string email, string password, CancellationToken cancellationToken = default)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            // Confirmed -> truly taken.
            if (existing.EmailConfirmed)
            {
                return Error.Conflict("User.EmailTaken", "This email is already registered.");
            }
            // Unconfirmed -> reuse it; the handler will send a fresh code.
            return existing.Id;
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            FullName = fullName,
            Email = email,
            UserName = email
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var description = string.Join(" ", result.Errors.Select(e => e.Description));
            return Error.Failure("User.CreationFailed", description);
        }

        return user.Id;
    }

    public async Task<Result<AuthUser>> VerifyEmailAsync(
    string email, string code, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return Error.NotFound("User.NotFound", "No account found for this email.");
        }

        var validation = await verificationCodeService.ValidateAsync(
            user.Id, code, VerificationPurpose.EmailConfirmation, cancellationToken);
        
        if (validation.IsFailure)
        {
            return validation.Error!;
        }

        user.EmailConfirmed = true;
        await userManager.UpdateAsync(user);

        return await BuildAuthUserAsync(user);
    }

    public async Task<Result<AuthUser>> LoginAsync(
        string email, string password, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null || !await userManager.CheckPasswordAsync(user, password))
        {
            return Error.Unauthorized("Auth.InvalidCredentials", "Invalid email or password.");
        }

        if (!user.EmailConfirmed)
        {
            return Error.Forbidden("Auth.EmailNotConfirmed", "email_not_confirmed");
        }

        return await BuildAuthUserAsync(user);
    }

    public async Task<Result<AuthUser>> ExternalSignInAsync(
        ExternalIdentity identity, CancellationToken cancellationToken = default)
    {
        // 1) Already linked.
        var linked = await userManager.FindByLoginAsync(identity.Provider, identity.Subject);
        if (linked is not null)
        {
            return await BuildAuthUserAsync(linked);
        }

        // Linking or creating by email is only safe when the provider vouches for the address.
        if (!identity.EmailVerified)
        {
            return Error.Unauthorized("Auth.ExternalEmailNotVerified", "The external account's email is not verified.");
        }

        var user = await userManager.FindByEmailAsync(identity.Email);
        if (user is not null)
        {
            // 2) Existing account with the same email -> link it.
            if (!user.EmailConfirmed)
            {
                // Whoever registered this unconfirmed account never proved owning the email:
                // drop their password so it cannot be used to enter the real owner's account.
                if (await userManager.HasPasswordAsync(user))
                {
                    var removed = await userManager.RemovePasswordAsync(user);
                    if (!removed.Succeeded) return IdentityFailure("Auth.ExternalLinkFailed", removed);
                }

                user.EmailConfirmed = true;
                var updated = await userManager.UpdateAsync(user);
                if (!updated.Succeeded) return IdentityFailure("Auth.ExternalLinkFailed", updated);
            }
        }
        else
        {
            // 3) New account without a password.
            user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                FullName = string.IsNullOrWhiteSpace(identity.FullName) ? identity.Email : identity.FullName.Trim(),
                Email = identity.Email,
                UserName = identity.Email,
                EmailConfirmed = true
            };

            var created = await userManager.CreateAsync(user);
            if (!created.Succeeded) return IdentityFailure("User.CreationFailed", created);
        }

        var login = await userManager.AddLoginAsync(user, new UserLoginInfo(identity.Provider, identity.Subject, identity.Provider));
        if (!login.Succeeded) return IdentityFailure("Auth.ExternalLinkFailed", login);

        return await BuildAuthUserAsync(user);
    }

    private static Error IdentityFailure(string code, IdentityResult result)
        => Error.Failure(code, string.Join(" ", result.Errors.Select(e => e.Description)));

    // Every token issue (login, verify, Google, refresh) goes through here, so the bootstrap
    // Admin grant takes effect without a restart, and a supervisor suspended everywhere is refused.
    private async Task<Result<AuthUser>> BuildAuthUserAsync(ApplicationUser user)
    {
        if (await userManager.IsLockedOutAsync(user))
        {
            return Error.Forbidden("Auth.AccountDisabled", "This account is disabled.");
        }
        await EnsureBootstrapAdminAsync(user);
        var roles = await userManager.GetRolesAsync(user);
        var block = await GetRoleBlockAsync(user.Id, roles);
        if (block is not null) return block;
        return new AuthUser(user.Id, user.Email!, user.FullName, roles.ToList(), user.MustChangePassword);
    }

    // Same checks as token issue, for a token that is already out (AccountBlockMiddleware):
    // a stopped company, a suspended supervisor or a disabled account lose access on the next request.
    public async Task<Error?> GetAccessBlockAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return null;
        if (await userManager.IsLockedOutAsync(user))
        {
            return Error.Forbidden("Auth.AccountDisabled", "This account is disabled.");
        }
        return await GetRoleBlockAsync(user.Id, await userManager.GetRolesAsync(user));
    }

    private async Task<Error?> GetRoleBlockAsync(Guid userId, IList<string> roles)
    {
        if (roles.Contains(Roles.Admin)) return null;
        var block = await GetCompanyBlockAsync(userId, roles);
        if (block is not null) return block;
        if (roles.Contains(Roles.Supervisor) && await IsSuspendedEverywhereAsync(userId))
        {
            return Error.Forbidden("Supervisor.Suspended", "This supervisor account is suspended by its company.");
        }
        return null;
    }

    private async Task<Error?> GetCompanyBlockAsync(Guid userId, IList<string> roles)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        if (roles.Contains(Roles.Company))
        {
            var owned = await context.Companies.AsNoTracking().Where(c => c.OwnerUserId == userId)
                .Select(c => new { c.IsStopped, c.ContractEndDate }).FirstOrDefaultAsync();
            if (owned is not null)
            {
                var status = Company.StatusOf(owned.IsStopped, owned.ContractEndDate, today);
                if (status == CompanyStatus.Stopped) return CompanyErrors.Stopped;
                if (status == CompanyStatus.Expired) return CompanyErrors.Expired;
            }
        }
        if (roles.Contains(Roles.Supervisor))
        {
            var rosters = await context.Companies.AsNoTracking()
                .Where(c => c.Supervisors.Any(s => s.UserId == userId))
                .Select(c => new { c.IsStopped, c.ContractEndDate }).ToListAsync();
            var statuses = rosters.Select(c => Company.StatusOf(c.IsStopped, c.ContractEndDate, today)).ToList();
            if (statuses.Count > 0 && statuses.All(s => s != CompanyStatus.Active))
                return statuses.All(s => s == CompanyStatus.Expired) ? CompanyErrors.Expired : CompanyErrors.Stopped;
        }
        return null;
    }

    private async Task<bool> IsSuspendedEverywhereAsync(Guid userId)
    {
        var entries = await context.Companies.AsNoTracking()
            .SelectMany(c => c.Supervisors)
            .Where(s => s.UserId == userId)
            .Select(s => s.IsSuspended).ToListAsync();
        return entries.Count > 0 && entries.All(suspended => suspended);
    }

    private async Task EnsureBootstrapAdminAsync(ApplicationUser user)
    {
        if (!user.EmailConfirmed || user.Email is null)
        {
            return;
        }

        var isConfigured = adminOptions.Value.Emails
            .Any(e => string.Equals(e?.Trim(), user.Email, StringComparison.OrdinalIgnoreCase));
        if (!isConfigured || await userManager.IsInRoleAsync(user, Roles.Admin))
        {
            return;
        }

        var result = await userManager.AddToRoleAsync(user, Roles.Admin);
        if (result.Succeeded)
        {
            logger.LogInformation("Granted bootstrap Admin role to user {UserId}.", user.Id);
        }
        else
        {
            logger.LogError("Could not grant bootstrap Admin role to {UserId}: {Errors}",
                user.Id, string.Join(" ", result.Errors.Select(e => e.Description)));
        }
    }

    public async Task<Result<UserAccountDto>> FindAccountByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(email);
        return user is null
            ? Error.NotFound("User.NotFound", "No account found for this email.")
            : await ToAccountDtoAsync(user);
    }

    public async Task<Result<UserAccountDto>> FindAccountByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user is null
            ? Error.NotFound("User.NotFound", "No account found for this id.")
            : await ToAccountDtoAsync(user);
    }

    public async Task<Result<ProvisionedAccount>> ProvisionAccountAsync(
        string fullName, string email, string? phoneNumber, string? role, string? requestedPassword = null,
        bool mustChangePassword = true, CancellationToken cancellationToken = default)
    {
        fullName = fullName.Trim();
        email = email.Trim();
        phoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();

        var user = await userManager.FindByEmailAsync(email);
        string? temporaryPassword = null;

        if (user is null)
        {
            temporaryPassword = requestedPassword ?? GenerateTemporaryPassword();
            user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                FullName = fullName,
                Email = email,
                UserName = email,
                PhoneNumber = phoneNumber,
                EmailConfirmed = true, // provisioned by a trusted admin/company: no OTP
                MustChangePassword = mustChangePassword
            };

            var created = await userManager.CreateAsync(user, temporaryPassword);
            if (!created.Succeeded) return IdentityFailure("User.CreationFailed", created);
        }
        else if (!user.EmailConfirmed)
        {
            // Whoever registered this account never proved owning the email: replace their password
            // with a temporary one sent to the real address (same anti-squatting rule as Google sign-in).
            temporaryPassword = requestedPassword ?? GenerateTemporaryPassword();
            if (await userManager.HasPasswordAsync(user))
            {
                var removed = await userManager.RemovePasswordAsync(user);
                if (!removed.Succeeded) return IdentityFailure("User.CreationFailed", removed);
            }

            var added = await userManager.AddPasswordAsync(user, temporaryPassword);
            if (!added.Succeeded) return IdentityFailure("User.CreationFailed", added);

            user.FullName = fullName;
            user.PhoneNumber = phoneNumber ?? user.PhoneNumber;
            user.EmailConfirmed = true;
            user.MustChangePassword = mustChangePassword;
            var updated = await userManager.UpdateAsync(user);
            if (!updated.Succeeded) return IdentityFailure("User.CreationFailed", updated);
            await refreshTokenService.RevokeAllForUserAsync(user.Id, cancellationToken);
        }

        if (role is not null && !await userManager.IsInRoleAsync(user, role))
        {
            var granted = await userManager.AddToRoleAsync(user, role);
            if (!granted.Succeeded) return IdentityFailure("User.RoleChangeFailed", granted);
        }

        logger.LogInformation("Provisioned account {UserId} (new: {IsNew}, role: {Role}).",
            user.Id, temporaryPassword is not null, role ?? "Pilgrim");
        return new ProvisionedAccount(user.Id, user.Email!, user.FullName, temporaryPassword);
    }

    // 14 characters with every class Identity may require (upper, lower, digit, symbol).
    private static string GenerateTemporaryPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#$%*?";
        const string all = upper + lower + digits + symbols;

        var chars = new char[14];
        chars[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        chars[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        chars[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        chars[3] = symbols[RandomNumberGenerator.GetInt32(symbols.Length)];
        for (var i = 4; i < chars.Length; i++)
        {
            chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        }

        RandomNumberGenerator.Shuffle(chars.AsSpan());
        return new string(chars);
    }

    public async Task<Result<UserAccountDto>> AddToRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Error.NotFound("User.NotFound", "No account found.");
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            var result = await userManager.AddToRoleAsync(user, role);
            if (!result.Succeeded) return IdentityFailure("User.RoleChangeFailed", result);
        }

        return await ToAccountDtoAsync(user);
    }

    public async Task<Result<UserAccountDto>> RemoveFromRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Error.NotFound("User.NotFound", "No account found.");
        }

        if (await userManager.IsInRoleAsync(user, role))
        {
            var result = await userManager.RemoveFromRoleAsync(user, role);
            if (!result.Succeeded) return IdentityFailure("User.RoleChangeFailed", result);
        }

        return await ToAccountDtoAsync(user);
    }

    private async Task<UserAccountDto> ToAccountDtoAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        return new UserAccountDto(user.Id, user.Email!, user.FullName, user.EmailConfirmed, roles.ToList());
    }

    public async Task<Result<AuthUser>> GetAuthUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user is null
            ? Error.NotFound("User.NotFound", "No account found.")
            : await BuildAuthUserAsync(user);
    }

    public async Task<Result<AuthUser>> ChangePasswordAsync(
        Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return Error.NotFound("User.NotFound", "No account found.");

        var changed = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!changed.Succeeded)
        {
            return changed.Errors.Any(e => e.Code == "PasswordMismatch")
                ? Error.Unauthorized("Auth.InvalidCredentials", "The current password is incorrect.")
                : IdentityFailure("Auth.ChangePasswordFailed", changed);
        }

        user.MustChangePassword = false;
        var updated = await userManager.UpdateAsync(user);
        if (!updated.Succeeded) return IdentityFailure("Auth.ChangePasswordFailed", updated);
        await refreshTokenService.RevokeAllForUserAsync(user.Id, cancellationToken);
        return await BuildAuthUserAsync(user);
    }

    public async Task<Result<string>> IssueTemporaryPasswordAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return Error.NotFound("User.NotFound", "No account found.");

        var password = GenerateTemporaryPassword();
        if (await userManager.HasPasswordAsync(user))
        {
            var removed = await userManager.RemovePasswordAsync(user);
            if (!removed.Succeeded) return IdentityFailure("User.PasswordResetFailed", removed);
        }
        var added = await userManager.AddPasswordAsync(user, password);
        if (!added.Succeeded) return IdentityFailure("User.PasswordResetFailed", added);

        user.MustChangePassword = true;
        var updated = await userManager.UpdateAsync(user);
        if (!updated.Succeeded) return IdentityFailure("User.PasswordResetFailed", updated);
        await refreshTokenService.RevokeAllForUserAsync(user.Id, cancellationToken);
        return password;
    }

    public async Task<Result<ProvisionedAccount>> IssueTemporaryPasswordAccountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var password = await IssueTemporaryPasswordAsync(userId, cancellationToken);
        if (password.IsFailure) return password.Error!;
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return Error.NotFound("User.NotFound", "No account found.");
        return new ProvisionedAccount(user.Id, user.Email!, user.FullName, password.Value);
    }

    public async Task<Result> SetDisabledAsync(Guid userId, bool disabled, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return Result.Failure(Error.NotFound("User.NotFound", "No account found."));
        await userManager.SetLockoutEnabledAsync(user, true);
        var updated = await userManager.SetLockoutEndDateAsync(user, disabled ? DateTimeOffset.MaxValue : null);
        if (!updated.Succeeded) return Result.Failure(IdentityFailure("Auth.AccountDisabled", updated));
        accessCache.Reset();
        if (disabled)
        {
            await refreshTokenService.RevokeAllForUserAsync(user.Id, cancellationToken);
            await notifier.AccountBlockedAsync(user.Id, "Auth.AccountDisabled", cancellationToken);
        }
        return Result.Success();
    }

    public async Task<Result> ResendEmailConfirmationAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return Result.Success(); // do not reveal existence
        }

        var purpose = user.EmailConfirmed ? VerificationPurpose.PasswordReset : VerificationPurpose.EmailConfirmation;
        return await verificationCodeService.GenerateAndSendAsync(
            user.Id, email, purpose, cancellationToken);
    }

    public async Task<Result> ForgotPasswordAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user is not null)
        {
            // A throttled request or a failed send is not reported (no user enumeration); a failed send
            // leaves no pending code, so the next request is not throttled.
            await verificationCodeService.GenerateAndSendAsync(
                user.Id, user.Email!, VerificationPurpose.PasswordReset, cancellationToken);
        }
        return Result.Success(); // always 200: no user enumeration
    }

    public async Task<Result> ResetPasswordAsync(
        string email, string code, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user is null)
        {
            return Result.Failure(Error.Failure("Otp.Invalid", "The verification code is incorrect."));
        }

        // Check the new password before using up the code: a weak password must not cost the user their code.
        foreach (var validator in userManager.PasswordValidators)
        {
            var check = await validator.ValidateAsync(userManager, user, newPassword);
            if (!check.Succeeded) return Result.Failure(Error.Validation("Auth.WeakPassword", string.Join(" ", check.Errors.Select(e => e.Description))));
        }

        var validation = await verificationCodeService.ValidateAsync(
            user.Id, code, VerificationPurpose.PasswordReset, cancellationToken);
        if (validation.IsFailure)
        {
            return validation;
        }

        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, resetToken, newPassword);
        if (!result.Succeeded)
        {
            return Result.Failure(Error.Failure("Auth.ResetFailed",
                string.Join(" ", result.Errors.Select(e => e.Description))));
        }

        user.MustChangePassword = false;
        user.EmailConfirmed = true; // the code reached this mailbox, so a never-verified signup can sign in now
        var updated = await userManager.UpdateAsync(user);
        if (!updated.Succeeded) return Result.Failure(IdentityFailure("Auth.ResetFailed", updated));

        await refreshTokenService.RevokeAllForUserAsync(user.Id, cancellationToken); // invalidate old sessions
        return Result.Success();
    }
}
