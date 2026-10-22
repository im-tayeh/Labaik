using System.Security.Cryptography;
using Labaik.Application.Common.Interfaces;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Identity;
using Labaik.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Labaik.Infrastructure.Identity;

internal sealed class VerificationCodeService(
    AppDbContext dbContext,
    IPasswordHasher<ApplicationUser> passwordHasher,
    IEmailSender emailSender,
    TimeProvider timeProvider,
    ILogger<VerificationCodeService> logger) : IVerificationCodeService
{
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(55);
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(15);
    private const int MaxAttempts = 5;

    public async Task<Result> GenerateAndSendAsync(
        Guid userId, string email, VerificationPurpose purpose, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        var activeCodes = await dbContext.VerificationCodes
            .Where(vc => vc.UserId == userId && vc.Purpose == purpose && !vc.IsUsed)
            .ToListAsync(cancellationToken);

        var mostRecent = activeCodes.MaxBy(vc => vc.CreatedAt);
        if (mostRecent is not null && now - mostRecent.CreatedAt < ResendCooldown)
        {
            return Result.Failure(Error.TooManyRequests("Otp.Throttled", "Please wait before requesting another code."));
        }

        foreach (var code in activeCodes) // invalidate older codes: only the newest is valid
        {
            code.MarkUsed();
        }

        var newCode = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var verificationCode = VerificationCode.Create(
            userId, passwordHasher.HashPassword(null!, newCode), purpose, now, now.Add(CodeLifetime));

        dbContext.VerificationCodes.Add(verificationCode);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await emailSender.SendAsync(
                email, "Your Labaik verification code",
                $"Your verification code is: {newCode}. It expires in {CodeLifetime.TotalMinutes:0} minutes.", cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Never delivered: drop it so the user can ask again right away (no cooldown) instead of a 500.
            logger.LogWarning(ex, "Sending the {Purpose} code failed.", purpose);
            verificationCode.MarkUsed();
            await dbContext.SaveChangesAsync(CancellationToken.None);
            return Result.Failure(Error.Failure("Email.SendFailed", "The verification email could not be sent. Try again."));
        }

        return Result.Success();
    }

    public async Task<Result> ValidateAsync(
        Guid userId, string code, VerificationPurpose purpose, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        var verificationCode = await dbContext.VerificationCodes
            .Where(vc => vc.UserId == userId && vc.Purpose == purpose && !vc.IsUsed)
            .OrderByDescending(vc => vc.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (verificationCode is null)
        {
            return Result.Failure(Error.Failure("Otp.Expired", "The verification code has expired. Request a new one."));
        }

        if (verificationCode.IsExpired(now))
        {
            return Result.Failure(Error.Failure("Otp.Expired", "The verification code has expired."));
        }

        if (verificationCode.AttemptCount >= MaxAttempts)
        {
            return Result.Failure(Error.Failure("Otp.TooManyAttempts", "Too many attempts. Request a new code."));
        }

        if (passwordHasher.VerifyHashedPassword(null!, verificationCode.CodeHash, code) == PasswordVerificationResult.Failed)
        {
            verificationCode.RegisterFailedAttempt();
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Failure(Error.Failure("Otp.Invalid", "The verification code is incorrect."));
        }

        verificationCode.MarkUsed();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
