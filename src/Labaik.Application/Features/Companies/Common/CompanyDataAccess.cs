using Labaik.Application.Common.Interfaces;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Companies;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Companies.Common;

internal static class CompanyDataAccess
{
    // Resolves the company owned by the current account (company endpoints act on "my company").
    public static async Task<Result<Guid>> GetMyCompanyIdAsync(
        this IAppDbContext context, ICurrentUser currentUser, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Error.Unauthorized("Auth.Required", "Authentication is required.");
        }

        var companyId = await context.Companies
            .Where(c => c.OwnerUserId == userId)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return companyId is { } id ? id : CompanyErrors.NotFound;
    }
}
