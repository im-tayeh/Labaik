using Labaik.Domain.Companies;

namespace Labaik.Application.Common.Interfaces;

public interface ICompanyServiceGate
{
    Task<bool> IsEnabledAsync(Guid userId, Func<CompanyServices, bool> service,
        CancellationToken cancellationToken = default);
}
