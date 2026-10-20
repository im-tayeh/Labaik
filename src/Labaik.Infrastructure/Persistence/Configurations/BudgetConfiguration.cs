using Labaik.Domain.Profiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Labaik.Infrastructure.Persistence.Configurations;

internal sealed class BudgetConfiguration : IEntityTypeConfiguration<Budget>
{
    public void Configure(EntityTypeBuilder<Budget> builder)
    {
        builder.ToTable("Budgets");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Currency).HasMaxLength(3).IsRequired();
        builder.HasIndex(b => b.UserId).IsUnique();
        builder.OwnsMany(b => b.Expenses, expenses =>
        {
            expenses.ToTable("BudgetExpenses");
            expenses.WithOwner().HasForeignKey("BudgetId");
            expenses.HasKey(e => e.Id);
            expenses.Property(e => e.Id).ValueGeneratedNever();
            expenses.Property(e => e.Category).HasMaxLength(100).IsRequired();
            expenses.Property(e => e.Note).HasMaxLength(500);
        });
        builder.Navigation(b => b.Expenses).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
