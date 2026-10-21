using Labaik.Application.Common.Interfaces;
using Labaik.Domain.Companies;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Common.Services;

public sealed class CompanyServiceGate(IAppDbContext context) : ICompanyServiceGate
{
    public async Task<bool> IsEnabledAsync(Guid userId, Func<CompanyServices, bool> service,
        CancellationToken cancellationToken = default)
    {
        var enabledServices = await context.Companies.AsNoTracking()
            .Where(c => c.OwnerUserId == userId
                || c.Supervisors.Any(s => s.UserId == userId)
                || context.Groups.Any(g => g.CompanyId == c.Id && g.Members.Any(m => m.UserId == userId)))
            .Select(c => c.EnabledServices)
            .ToListAsync(cancellationToken);
        return enabledServices.Count == 0 || enabledServices.Any(service);
    }
}
