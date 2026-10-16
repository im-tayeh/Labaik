namespace Labaik.Application.Common.Models;

public sealed record AccessToken(string Value, int ExpiresInSeconds);

public sealed record AuthUser(Guid Id, string Email, string FullName, IReadOnlyList<string> Roles);

// Roles: empty for a pilgrim; "Supervisor" and/or "Admin" otherwise (drives screen gating in the app).
public sealed record AuthUserSummary(Guid Id, string Email, string FullName, IReadOnlyList<string> Roles);

// Admin view of an account.
public sealed record UserAccountDto(Guid Id, string Email, string FullName, bool EmailConfirmed, IReadOnlyList<string> Roles);

// A verified identity from an external provider (e.g. Google). Subject is the provider's stable user id.
public sealed record ExternalIdentity(string Provider, string Subject, string Email, bool EmailVerified, string? FullName);

public sealed record AuthTokens(string AccessToken, string RefreshToken, string TokenType, int ExpiresIn, AuthUserSummary User);