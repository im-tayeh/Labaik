using Labaik.Domain.Common.Results;

namespace Labaik.Application.Common.Interfaces;

public interface IRefreshTokenService
{
    Task<string> IssueAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result<Guid>> ValidateAndConsumeAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}