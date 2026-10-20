using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Profiles;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Profiles.Budget;

public sealed record ReplaceMyBudgetCommand(decimal TotalAmount, string Currency, IReadOnlyList<ExpenseRequest> Expenses)
    : IRequest<Result<BudgetDto>>;

public sealed class ReplaceMyBudgetCommandValidator : AbstractValidator<ReplaceMyBudgetCommand>
{
    public ReplaceMyBudgetCommandValidator()
    {
        RuleFor(x => x.TotalAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3).Matches("^[A-Za-z]{3}$");
        RuleForEach(x => x.Expenses).ChildRules(expense =>
        {
            expense.RuleFor(x => x.Amount).GreaterThan(0);
            expense.RuleFor(x => x.Category).NotEmpty().MaximumLength(100);
            expense.RuleFor(x => x.Note).MaximumLength(500);
        });
    }
}

public sealed class ReplaceMyBudgetCommandHandler(IAppDbContext context, ICurrentUser currentUser)
    : IRequestHandler<ReplaceMyBudgetCommand, Result<BudgetDto>>
{
    public async Task<Result<BudgetDto>> Handle(ReplaceMyBudgetCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId) return Error.Unauthorized("Auth.Required", "Authentication is required.");
        var budget = await context.Budgets.Include(b => b.Expenses)
            .FirstOrDefaultAsync(b => b.UserId == userId, cancellationToken);
        if (budget is null)
        {
            budget = Labaik.Domain.Profiles.Budget.Create(userId, 0, "SAR", []);
            context.Budgets.Add(budget);
        }

        var replaced = budget.Replace(request.TotalAmount, request.Currency,
            request.Expenses.Select(e => new ExpenseInput(e.Amount, e.Category, e.Note, e.SpentAtUtc)));
        if (replaced.IsFailure) return replaced.Error!;
        await context.SaveChangesAsync(cancellationToken);
        return BudgetDto.From(budget);
    }
}
