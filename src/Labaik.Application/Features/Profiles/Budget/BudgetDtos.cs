using Labaik.Domain.Profiles;

namespace Labaik.Application.Features.Profiles.Budget;

public sealed record ExpenseDto(Guid Id, decimal Amount, string Category, string? Note, DateTimeOffset SpentAtUtc);
public sealed record BudgetDto(decimal TotalAmount, string Currency, IReadOnlyList<ExpenseDto> Expenses)
{
    public static BudgetDto Default { get; } = new(0, "SAR", []);
    public static BudgetDto From(Labaik.Domain.Profiles.Budget budget) => new(budget.TotalAmount, budget.Currency,
        budget.Expenses.Select(expense => new ExpenseDto(expense.Id, expense.Amount, expense.Category, expense.Note, expense.SpentAtUtc)).ToList());
}

public sealed record ExpenseRequest(decimal Amount, string Category, string? Note, DateTimeOffset SpentAtUtc);
