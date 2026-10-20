using Labaik.Domain.Rituals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Labaik.Infrastructure.Persistence.Configurations;

internal sealed class RitualProgressConfiguration : IEntityTypeConfiguration<RitualProgress>
{
    public void Configure(EntityTypeBuilder<RitualProgress> builder)
    {
        builder.ToTable("RitualProgresses");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.UserId).IsRequired();
        builder.Property(p => p.CurrentStepKey).HasMaxLength(100);
        builder.HasIndex(p => new { p.UserId, p.PilgrimageType }).IsUnique(); // one progress per user and type

        // Completed steps are appended to an already-saved aggregate across requests.
        // The Guid key is client-generated, so ValueGeneratedNever makes EF treat new items as inserts
        // (otherwise a set key looks like an existing row -> UPDATE of 0 rows -> DbUpdateConcurrencyException).
        builder.OwnsMany(p => p.CompletedSteps, c =>
        {
            c.ToTable("CompletedRitualSteps");
            c.WithOwner().HasForeignKey("RitualProgressId");
            c.HasKey(x => x.Id);
            c.Property(x => x.Id).ValueGeneratedNever();
            c.HasIndex("RitualProgressId", nameof(CompletedRitualStep.StepOrder)).IsUnique();
        });

        builder.Navigation(p => p.CompletedSteps).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
