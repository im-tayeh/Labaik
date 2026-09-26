using Labaik.Domain.Groups;
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
        builder.HasIndex(g => g.JoinCode).IsUnique(); // enforce uniqueness at the DB level

        builder.OwnsMany(g => g.Members, m =>
        {
            m.ToTable("GroupMembers");
            m.WithOwner().HasForeignKey("GroupId");
            m.HasKey(x => x.Id);
            m.HasIndex(nameof(GroupMember.UserId), "GroupId").IsUnique(); // no double-join
        });

        builder.Navigation(g => g.Members).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}