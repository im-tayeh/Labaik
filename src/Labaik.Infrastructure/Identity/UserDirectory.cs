using Labaik.Application.Common.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Infrastructure.Identity;

internal sealed class UserDirectory(UserManager<ApplicationUser> userManager) : IUserDirectory
{
    public async Task<string?> GetFullNameAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user?.FullName;
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetFullNamesAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
        => await userManager.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);
}
