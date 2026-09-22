using Labaik.Application.Common.Interfaces;
using Labaik.Application.Common.Models;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Identity;
using Microsoft.AspNetCore.Identity;

namespace Labaik.Infrastructure.Identity;

internal sealed class IdentityService(
    UserManager<ApplicationUser> userManager,
    IVerificationCodeService verificationCodeService,
    IRefreshTokenService refreshTokenService) : IIdentityService
{
    public async Task<Result<Guid>> CreateUserAsync(string fullName, string email, string password, CancellationToken cancellationToken = default)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            return Error.Conflict("User.EmailTaken", "This email is already registered.");
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

    private async Task<AuthUser> BuildAuthUserAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        return new AuthUser(user.Id, user.Email!, user.FullName, roles.ToList());
    }

    public async Task<Result<AuthUser>> GetAuthUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user is null
            ? Error.NotFound("User.NotFound", "No account found.")
            : await BuildAuthUserAsync(user);
    }

    public async Task<Result> ResendEmailConfirmationAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null || user.EmailConfirmed)
        {
            return Result.Success(); // do not reveal existence / nothing to resend
        }
        return await verificationCodeService.GenerateAndSendAsync(
            user.Id, email, VerificationPurpose.EmailConfirmation, cancellationToken);
    }

    public async Task<Result> ForgotPasswordAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is not null)
        {
            await verificationCodeService.GenerateAndSendAsync(
                user.Id, email, VerificationPurpose.PasswordReset, cancellationToken);
        }
        return Result.Success(); // always 200: no user enumeration
    }

    public async Task<Result> ResetPasswordAsync(
        string email, string code, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return Result.Failure(Error.Failure("Otp.Invalid", "The verification code is incorrect."));
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

        await refreshTokenService.RevokeAllForUserAsync(user.Id, cancellationToken); // invalidate old sessions
        return Result.Success();
    }
}