using Labaik.Domain.Common;
using Labaik.Domain.Common.Results;

namespace Labaik.Domain.Profiles;

public sealed class Budget : AggregateRoot
{
    public Guid UserId { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string Currency { get; private set; } = "SAR";
    private readonly List<Expense> _expenses = [];
    public IReadOnlyList<Expense> Expenses => _expenses.AsReadOnly();

    private Budget() { }

    public static Budget Create(Guid userId, decimal totalAmount, string currency, IEnumerable<ExpenseInput> expenses)
    {
        var budget = new Budget { UserId = userId };
        budget.Replace(totalAmount, currency, expenses);
        return budget;
    }

    public Result Replace(decimal totalAmount, string currency, IEnumerable<ExpenseInput> expenses)
    {
        currency = currency?.Trim().ToUpperInvariant() ?? string.Empty;
        var inputs = expenses.ToList();
        if (UserId == Guid.Empty || totalAmount < 0 || currency.Length != 3 || !currency.All(char.IsAsciiLetterUpper))
            return Result.Failure(Error.Validation("Budget.Invalid", "Budget amount or currency is invalid."));
        if (inputs.Any(e => e.Amount <= 0 || string.IsNullOrWhiteSpace(e.Category) || e.Category.Trim().Length > 100
            || e.Note?.Length > 500))
            return Result.Failure(Error.Validation("Budget.InvalidExpense", "Expense amount, category, or note is invalid."));

        TotalAmount = totalAmount;
        Currency = currency;
        _expenses.Clear();
        _expenses.AddRange(inputs.Select(e => Expense.Create(e.Amount, e.Category.Trim(), e.Note?.Trim(), e.SpentAtUtc)));
        return Result.Success();
    }
}

public sealed class Expense
{
    public Guid Id { get; private set; }
    public decimal Amount { get; private set; }
    public string Category { get; private set; } = string.Empty;
    public string? Note { get; private set; }
    public DateTimeOffset SpentAtUtc { get; private set; }

    private Expense() { }

    internal static Expense Create(decimal amount, string category, string? note, DateTimeOffset spentAtUtc)
        => new() { Id = Guid.NewGuid(), Amount = amount, Category = category, Note = note, SpentAtUtc = spentAtUtc };
}

public sealed record ExpenseInput(decimal Amount, string Category, string? Note, DateTimeOffset SpentAtUtc);
