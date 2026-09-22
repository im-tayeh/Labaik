using Labaik.Application.Common.Interfaces;
using Labaik.Application.Common.Models;

namespace Labaik.Application.Common.Authentication;

internal sealed class AuthTokenFactory(
    IJwtTokenGenerator jwtTokenGenerator,
    IRefreshTokenService refreshTokenService) : IAuthTokenFactory
{
    public async Task<AuthTokens> CreateAsync(AuthUser user, CancellationToken cancellationToken = default)
    {
        var accessToken = jwtTokenGenerator.GenerateAccessToken(user.Id, user.Email, user.FullName, user.Roles);
        var refreshToken = await refreshTokenService.IssueAsync(user.Id, cancellationToken);

        return new AuthTokens(
            accessToken.Value, refreshToken, "Bearer", accessToken.ExpiresInSeconds,
            new AuthUserSummary(user.Id, user.Email, user.FullName));
    }
}