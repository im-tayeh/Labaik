using Labaik.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Labaik.Infrastructure.Persistence.Configurations;

// Every refresh looks a token up by its hash and every sign-out/suspension revokes by user; every OTP
// check looks codes up by user and purpose. Without these the tables (which only grow) are scanned.
internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasIndex(t => t.TokenHash);
        builder.HasIndex(t => t.UserId);
    }
}

internal sealed class VerificationCodeConfiguration : IEntityTypeConfiguration<VerificationCode>
{
    public void Configure(EntityTypeBuilder<VerificationCode> builder)
        => builder.HasIndex(c => new { c.UserId, c.Purpose });
}
