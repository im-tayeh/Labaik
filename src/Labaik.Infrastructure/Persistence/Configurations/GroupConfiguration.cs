using Labaik.Domain.Companies;
using Labaik.Domain.Groups;
using Labaik.Domain.Groups.ValueObjects;
using Labaik.Domain.Trips;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Labaik.Infrastructure.Persistence.Configurations;

internal sealed class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.ToTable("Groups");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Name).HasMaxLength(100).IsRequired();
        builder.Property(g => g.JoinCode).HasMaxLength(20).IsRequired();
        builder.Property(g => g.Notes).HasMaxLength(Group.NotesMaxLength);
        builder.HasIndex(g => g.JoinCode).IsUnique(); // enforce uniqueness at the DB level
        builder.HasIndex(g => g.LeaderId); // /supervisor/groups, supervisor checks

        // Gathering point: value object in the group row (all columns null until set).
        builder.OwnsOne(g => g.GatheringPoint, p =>
        {
            p.Property(x => x.Address).HasMaxLength(GatheringPoint.AddressMaxLength);
        });

        // Company-managed groups reference their company and trip by id (nullable for legacy groups).
        builder.HasOne<Company>().WithMany().HasForeignKey(g => g.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Trip>().WithMany().HasForeignKey(g => g.TripId).OnDelete(DeleteBehavior.Restrict);

        builder.OwnsMany(g => g.Members, m =>
        {
            m.ToTable("GroupMembers");
            m.WithOwner().HasForeignKey("GroupId");
            m.HasKey(x => x.Id);
            m.Property(x => x.Id).ValueGeneratedNever(); // new members on a saved group must INSERT, not UPDATE
            m.HasIndex(nameof(GroupMember.UserId), "GroupId").IsUnique(); // no double-join
            m.Property(x => x.Notes).HasMaxLength(GroupMember.NotesMaxLength);
        });

        builder.Navigation(g => g.Members).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
