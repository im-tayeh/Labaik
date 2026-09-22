using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Common.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Labaik.Infrastructure.Authentication;

internal sealed class JwtTokenGenerator(
    IOptions<JwtSettings> jwtOptions,
    TimeProvider timeProvider) : IJwtTokenGenerator
{
    private readonly JwtSettings _settings = jwtOptions.Value;

    public AccessToken GenerateAccessToken(
        Guid userId, string email, string fullName, IEnumerable<string> roles)
    {
        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Name, fullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(_settings.AccessTokenExpiryMinutes);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: signingCredentials);

        var value = new JwtSecurityTokenHandler().WriteToken(token);
        return new AccessToken(value, _settings.AccessTokenExpiryMinutes * 60);
    }
}