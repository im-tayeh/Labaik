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

    // A tracked company-managed group with its members, or null when it is not this company's.
    public static Task<Group?> GetCompanyGroupAsync(
        this IAppDbContext context, Guid groupId, Guid companyId, CancellationToken cancellationToken)
        => context.Groups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == groupId && g.CompanyId == companyId, cancellationToken);
}
