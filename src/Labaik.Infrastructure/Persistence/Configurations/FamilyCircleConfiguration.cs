using Labaik.Domain.FamilyCircles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Labaik.Infrastructure.Persistence.Configurations;

internal sealed class FamilyCircleConfiguration : IEntityTypeConfiguration<FamilyCircle>
{
    public void Configure(EntityTypeBuilder<FamilyCircle> builder)
    {
        builder.ToTable("FamilyCircles");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasMaxLength(FamilyCircle.NameMaxLength).IsRequired();
        builder.Property(c => c.JoinCode).HasMaxLength(20).IsRequired();
        builder.HasIndex(c => c.JoinCode).IsUnique();

        builder.OwnsMany(c => c.Members, m =>
        {
            m.ToTable("FamilyCircleMembers");
            m.WithOwner().HasForeignKey("CircleId");
            m.HasKey(x => x.Id);
            m.Property(x => x.Id).ValueGeneratedNever(); // new members on a saved circle must INSERT, not UPDATE
            m.HasIndex(nameof(FamilyCircleMember.UserId), "CircleId").IsUnique();
        });
        builder.Navigation(c => c.Members).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
