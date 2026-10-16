using Labaik.Application.Common.Models;
using Labaik.Domain.Common.Results;

namespace Labaik.Application.Common.Interfaces;

// Validates a Google ID token (signature, expiry, audience = one of our client ids).
public interface IGoogleTokenValidator
{
    Task<Result<ExternalIdentity>> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}
