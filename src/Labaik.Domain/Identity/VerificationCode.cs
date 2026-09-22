namespace Labaik.Domain.Identity;

public enum VerificationPurpose
{
    EmailConfirmation,
    PasswordReset
}

public sealed class VerificationCode
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public string CodeHash { get; private set; } = string.Empty;
    public VerificationPurpose Purpose { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public int AttemptCount { get; private set; }
    public bool IsUsed { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private VerificationCode() { }

    public static VerificationCode Create(
    Guid userId, string codeHash, VerificationPurpose purpose, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    => new()
    {
        UserId = userId,
        CodeHash = codeHash,
        Purpose = purpose,
        CreatedAt = createdAt,
        ExpiresAt = expiresAt
    };

    public void MarkUsed() => IsUsed = true;
    public void RegisterFailedAttempt() => AttemptCount++;
    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;
}