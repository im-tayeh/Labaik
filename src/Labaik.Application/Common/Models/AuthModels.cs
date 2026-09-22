namespace Labaik.Application.Common.Models;

public sealed record AccessToken(string Value, int ExpiresInSeconds);

public sealed record AuthUser(Guid Id, string Email, string FullName, IReadOnlyList<string> Roles);

public sealed record AuthUserSummary(Guid Id, string Email, string FullName);

public sealed record AuthTokens(string AccessToken, string RefreshToken, string TokenType, int ExpiresIn, AuthUserSummary User);