using Labaik.Domain.Chat;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Labaik.Infrastructure.Persistence.Configurations;

internal sealed class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("ChatMessages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Body).HasMaxLength(ChatErrors.BodyMaxLength).IsRequired();
        builder.HasIndex(m => new { m.GroupId, m.SentAtUtc }); // group feed, newest first, cursor paging
    }
}
