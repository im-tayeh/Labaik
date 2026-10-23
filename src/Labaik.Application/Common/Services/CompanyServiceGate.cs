using Labaik.Application.Common.Interfaces;
using Labaik.Domain.Companies;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Common.Services;

// The linked companies' services are read once per user per scope (one request), so checking several
// services (e.g. /home's five flags) costs one query, not one per flag.
public sealed class CompanyServiceGate(IAppDbContext context) : ICompanyServiceGate
{
    private readonly Dictionary<Guid, List<CompanyServices>> _byUser = [];

    public async Task<bool> IsEnabledAsync(Guid userId, Func<CompanyServices, bool> service,
        CancellationToken cancellationToken = default)
    {
        if (!_byUser.TryGetValue(userId, out var enabledServices))
        {
            enabledServices = await LoadAsync(userId, cancellationToken);
            _byUser[userId] = enabledServices;
        }
        return enabledServices.Count == 0 || enabledServices.Any(service);
    }

    private Task<List<CompanyServices>> LoadAsync(Guid userId, CancellationToken cancellationToken) =>
        context.Companies.AsNoTracking()
            .Where(c => c.OwnerUserId == userId
                || c.Supervisors.Any(s => s.UserId == userId)
                || context.Groups.Any(g => g.CompanyId == c.Id && g.Members.Any(m => m.UserId == userId)))
            .Select(c => c.EnabledServices)
            .ToListAsync(cancellationToken);
}
