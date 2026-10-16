using Labaik.Domain.Locations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Labaik.Infrastructure.Persistence.Configurations;

internal sealed class LocationShareConfiguration : IEntityTypeConfiguration<LocationShare>
{
    public void Configure(EntityTypeBuilder<LocationShare> builder)
    {
        builder.ToTable("LocationShares");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.UserId).IsRequired();
        builder.HasIndex(s => s.UserId).IsUnique(); // one live location per user (no history)
    }
}
