using Labaik.Application.Common.Models;

namespace Labaik.Application.Common.Interfaces;

public interface IAuthTokenFactory
{
    Task<AuthTokens> CreateAsync(AuthUser user, CancellationToken cancellationToken = default);
}