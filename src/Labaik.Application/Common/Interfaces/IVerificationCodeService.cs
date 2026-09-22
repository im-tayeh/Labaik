using Labaik.Domain.Common.Results;
using Labaik.Domain.Identity;

namespace Labaik.Application.Common.Interfaces;

public interface IVerificationCodeService
{
    Task<Result> GenerateAndSendAsync(Guid userId, string email, VerificationPurpose purpose, CancellationToken cancellationToken = default);
    Task<Result> ValidateAsync(Guid userId, string code, VerificationPurpose purpose, CancellationToken cancellationToken = default);
}