using System.Security.Cryptography;
using System.Text;
using Labaik.Application.Common.Interfaces;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Identity;
using Labaik.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Labaik.Infrastructure.Authentication;

internal sealed class RefreshTokenService(
    AppDbContext dbContext,
    TimeProvider timeProvider,
    IOptions<JwtSettings> jwtOptions) : IRefreshTokenService
{
    private readonly JwtSettings _settings = jwtOptions.Value;

    public async Task<string> IssueAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var now = timeProvider.GetUtcNow();

        var refreshToken = RefreshToken.Create(
            userId, Hash(rawToken), now, now.AddDays(_settings.RefreshTokenExpiryDays));

        dbContext.RefreshTokens.Add(refreshToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return rawToken;
    }

    public async Task<Result<Guid>> ValidateAndConsumeAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var hash = Hash(refreshToken);

        var stored = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == hash, cancellationToken);

        if (stored is null || !stored.IsActive(now))
        {
            return Error.Unauthorized("Auth.InvalidRefreshToken", "The refresh token is invalid or expired.");
        }

        stored.Revoke(now); // rotation: single use
        await dbContext.SaveChangesAsync(cancellationToken);

        return stored.UserId;
    }

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var tokens = await dbContext.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.Revoke(now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}