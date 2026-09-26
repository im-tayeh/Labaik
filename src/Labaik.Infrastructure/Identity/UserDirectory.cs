using Labaik.Application.Common.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace Labaik.Infrastructure.Identity;

internal sealed class UserDirectory(UserManager<ApplicationUser> userManager) : IUserDirectory
{
    public async Task<string?> GetFullNameAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user?.FullName;
    }
}