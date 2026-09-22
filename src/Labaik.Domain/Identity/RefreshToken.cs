namespace Labaik.Domain.Identity;

public sealed class RefreshToken
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    private RefreshToken() { }

    public static RefreshToken Create(Guid userId, string tokenHash, DateTimeOffset createdAt, DateTimeOffset expiresAt)
        => new() { UserId = userId, TokenHash = tokenHash, CreatedAt = createdAt, ExpiresAt = expiresAt };

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;
    public void Revoke(DateTimeOffset now) => RevokedAt = now;
}