using Labaik.Domain.Rituals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Labaik.Infrastructure.Persistence.Configurations;

internal sealed class RitualStepConfiguration : IEntityTypeConfiguration<RitualStep>
{
    public void Configure(EntityTypeBuilder<RitualStep> builder)
    {
        builder.ToTable("RitualSteps");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Title).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Body).IsRequired();
        builder.Property(s => s.Reference).HasMaxLength(500);
        builder.HasIndex(s => new { s.PilgrimageType, s.Order }).IsUnique(); // one step per position
    }
}
