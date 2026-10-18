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
    // Finds the user linked to the external login, else links by verified email, else creates a user.
    Task<Result<AuthUser>> ExternalSignInAsync(ExternalIdentity identity, CancellationToken cancellationToken = default);
    Task<Result<UserAccountDto>> FindAccountByEmailAsync(string email, CancellationToken cancellationToken = default);
    // Top-down account creation (admin/company): a new email gets a confirmed account with a temporary
    // password; a confirmed existing account is reused as is (no password change). An unconfirmed
    // existing account never proved its email, so it is taken over like a new one. Role null = pilgrim.
    Task<Result<ProvisionedAccount>> ProvisionAccountAsync(
        string fullName, string email, string? phoneNumber, string? role, CancellationToken cancellationToken = default);
    Task<Result<UserAccountDto>> AddToRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<UserAccountDto>> RemoveFromRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result> ResetPasswordAsync(string email, string code, string newPassword, CancellationToken cancellationToken = default);
}