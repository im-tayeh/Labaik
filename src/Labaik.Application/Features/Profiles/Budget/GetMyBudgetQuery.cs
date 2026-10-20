using Labaik.Application.Common.Interfaces;
using Labaik.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Profiles.Budget;

public sealed record GetMyBudgetQuery : IRequest<Result<BudgetDto>>;

public sealed class GetMyBudgetQueryHandler(IAppDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetMyBudgetQuery, Result<BudgetDto>>
{
    public async Task<Result<BudgetDto>> Handle(GetMyBudgetQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId) return Error.Unauthorized("Auth.Required", "Authentication is required.");
        var budget = await context.Budgets.Include(b => b.Expenses).AsNoTracking()
            .FirstOrDefaultAsync(b => b.UserId == userId, cancellationToken);
        return budget is null ? BudgetDto.Default : BudgetDto.From(budget);
    }
}
