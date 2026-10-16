using Labaik.Domain.Guides;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Labaik.Infrastructure.Persistence.Configurations;

internal sealed class GuideArticleConfiguration : IEntityTypeConfiguration<GuideArticle>
{
    public void Configure(EntityTypeBuilder<GuideArticle> builder)
    {
        builder.ToTable("GuideArticles");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Title).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Body).IsRequired();
        builder.Property(a => a.Reference).HasMaxLength(500);
        builder.HasIndex(a => new { a.Category, a.Order }).IsUnique(); // one article per position
    }
}
