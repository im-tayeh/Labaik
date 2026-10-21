using Labaik.Domain.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Labaik.Infrastructure.Persistence.Configurations;

internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies");
        builder.HasKey(c => c.Id);
        builder.HasQueryFilter(c => c.DeletedAtUtc == null);
        builder.Property(c => c.Name).HasMaxLength(CompanyErrors.NameMaxLength).IsRequired();
        builder.HasIndex(c => c.OwnerUserId).IsUnique(); // one company per owner account

        builder.Property(c => c.CommercialRegistrationNumber)
            .HasMaxLength(CompanyErrors.RegistrationNumberMaxLength).IsRequired().HasDefaultValue(string.Empty);
        // Unique among real values; companies created before the field existed have an empty one.
        builder.HasIndex(c => c.CommercialRegistrationNumber).IsUnique()
            .HasFilter("\"CommercialRegistrationNumber\" <> ''");

        builder.Property(c => c.OfficialEmail).HasMaxLength(CompanyErrors.EmailMaxLength);
        builder.Property(c => c.PrimaryPhone).HasMaxLength(CompanyErrors.PhoneMaxLength);
        builder.Property(c => c.LogoUrl).HasMaxLength(CompanyErrors.UrlMaxLength);
        builder.Property(c => c.Country).HasMaxLength(CompanyErrors.PlaceMaxLength);
        builder.Property(c => c.City).HasMaxLength(CompanyErrors.PlaceMaxLength);
        builder.Property(c => c.VatId).HasMaxLength(CompanyErrors.ReferenceMaxLength);
        builder.Property(c => c.ContractReferenceNumber).HasMaxLength(CompanyErrors.ReferenceMaxLength);
        builder.Property(c => c.SubscriptionPrice).HasPrecision(12, 2);
        builder.Property(c => c.ContractFileUrl).HasMaxLength(CompanyErrors.UrlMaxLength);
        builder.Property(c => c.ContactFullName)
            .HasMaxLength(CompanyErrors.ContactNameMaxLength).IsRequired().HasDefaultValue(string.Empty);
        builder.Property(c => c.ContactJobTitle).HasMaxLength(CompanyErrors.JobTitleMaxLength);
        builder.Property(c => c.ContactMobile).HasMaxLength(CompanyErrors.PhoneMaxLength);

        // Service flags as plain bool columns on the company row.
        builder.OwnsOne(c => c.EnabledServices, s =>
        {
            s.Property(x => x.GpsTracking).HasColumnName("ServiceGpsTracking");
            s.Property(x => x.DuaAndRituals).HasColumnName("ServiceDuaAndRituals");
            s.Property(x => x.InstantNotifications).HasColumnName("ServiceInstantNotifications");
            s.Property(x => x.ChatWithGuides).HasColumnName("ServiceChatWithGuides");
            s.Property(x => x.ReportsAndStats).HasColumnName("ServiceReportsAndStats");
        });
        builder.Navigation(c => c.EnabledServices).IsRequired();

        builder.OwnsMany(c => c.Supervisors, s =>
        {
            s.ToTable("CompanySupervisors");
            s.WithOwner().HasForeignKey("CompanyId");
            s.HasKey(x => x.Id);
            s.Property(x => x.Id).ValueGeneratedNever(); // new rows on a saved company must INSERT, not UPDATE
            s.Property(x => x.ProvisionedByCompanyId);
            s.HasIndex(nameof(CompanySupervisor.UserId), "CompanyId").IsUnique();
        });
        builder.Navigation(c => c.Supervisors).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
