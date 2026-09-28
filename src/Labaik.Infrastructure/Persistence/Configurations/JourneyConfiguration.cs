using Labaik.Domain.Journeys;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Labaik.Infrastructure.Persistence.Configurations;

internal sealed class JourneyConfiguration : IEntityTypeConfiguration<Journey>
{
    public void Configure(EntityTypeBuilder<Journey> builder)
    {
        builder.ToTable("Journeys");
        builder.HasKey(j => j.Id);
        builder.Property(j => j.UserId).IsRequired();

        // ArrivalPlan is a value object -> owned (stored in the same row, nullable).
        builder.OwnsOne(j => j.ArrivalPlan, a =>
        {
            a.Property(p => p.Mode);
            a.Property(p => p.EntryPoint).HasMaxLength(200);
        });

        // Stays are part of the aggregate -> owned collection in their own table.
        builder.OwnsMany(j => j.Stays, s =>
        {
            s.ToTable("JourneyStays");
            s.WithOwner().HasForeignKey("JourneyId");
            s.HasKey(x => x.Id);   // use the existing Guid key
            s.Property(x => x.CityName).HasMaxLength(100);
            s.Property(x => x.PlaceName).HasMaxLength(200);
        });

        builder.Navigation(j => j.Stays).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}