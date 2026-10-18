using Labaik.Domain.HelpCenter;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Labaik.Infrastructure.Persistence.Configurations;

internal sealed class LostChildReportConfiguration : IEntityTypeConfiguration<LostChildReport>
{
    public void Configure(EntityTypeBuilder<LostChildReport> builder)
    {
        builder.ToTable("LostChildReports");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Description).HasMaxLength(LostChildReport.DescriptionMaxLength).IsRequired();
        builder.Property(r => r.LocationDescription).HasMaxLength(LostChildReport.LocationDescriptionMaxLength);
        builder.Property(r => r.PhotoUrl).HasMaxLength(LostChildReport.PhotoUrlMaxLength);
        builder.HasIndex(r => new { r.ReporterId, r.CreatedAtUtc });
    }
}
