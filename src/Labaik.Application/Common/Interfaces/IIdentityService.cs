using Labaik.Application.Common.Models;
using Labaik.Domain.Common.Results;

namespace Labaik.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<Result<Guid>> CreateUserAsync(
        string fullName, string email, string password, CancellationToken cancellationToken = default);
    Task<Result<AuthUser>> VerifyEmailAsync(string email, string code, CancellationToken cancellationToken = default);
    Task<Result<AuthUser>> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<Result<AuthUser>> GetAuthUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result> ResendEmailConfirmationAsync(string email, CancellationToken cancellationToken = default);
    Task<Result> ForgotPasswordAsync(string email, CancellationToken cancellationToken = default);
    Task<Result> ResetPasswordAsync(string email, string code, string newPassword, CancellationToken cancellationToken = default);
}