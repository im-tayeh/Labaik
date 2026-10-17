using Labaik.Application.Common.Interfaces;
using Labaik.Domain.Groups;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Groups.Common;

internal static class GroupDataAccess
{
    // A unique join code (retry on the rare collision; the unique index is the final guard).
    public static async Task<string> GenerateUniqueJoinCodeAsync(
        this IAppDbContext context, IJoinCodeGenerator codeGenerator, CancellationToken cancellationToken)
    {
        string code;
        var attempts = 0;
        do
        {
            code = codeGenerator.Generate();
            attempts++;
        }
        while (await context.Groups.AnyAsync(g => g.JoinCode == code, cancellationToken) && attempts < 5);

        return code;
    }

    // A tracked group with its members, or null when it does not exist or the user is not a member
    // (callers answer NotFound either way: never leak a group's existence).
    public static async Task<Group?> GetGroupForMemberAsync(
        this IAppDbContext context, Guid groupId, Guid userId, CancellationToken cancellationToken)
    {
        var group = await context.Groups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken);

        return group is not null && group.IsMember(userId) ? group : null;
    }

    // A tracked company-managed group with its members, or null when it is not this company's.
    public static Task<Group?> GetCompanyGroupAsync(
        this IAppDbContext context, Guid groupId, Guid companyId, CancellationToken cancellationToken)
        => context.Groups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == groupId && g.CompanyId == companyId, cancellationToken);
}
