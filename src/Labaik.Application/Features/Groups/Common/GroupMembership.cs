using Labaik.Application.Common.Interfaces;
using Labaik.Application.Common.Provisioning;
using Labaik.Application.Features.Companies.Common;
using Labaik.Application.Features.Groups.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Groups;
using Labaik.Domain.Groups.Enums;
using Labaik.Domain.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Labaik.Application.Features.Groups.Common;

internal static class GroupMembership
{
    public static async Task<Result<CompanyGroupDto>> AddByEmailAsync(
        Group group, string email, string? fullName, string? phoneNumber, Guid? addedBySupervisorId,
        IAppDbContext context, IIdentityService identityService, IUserDirectory userDirectory,
        IEmailSender emailSender, IOptions<AppLinksSettings> appLinks, TimeProvider timeProvider,
        ILogger logger, CancellationToken cancellationToken)
    {
        email = email.Trim();
        fullName = string.IsNullOrWhiteSpace(fullName) ? email : fullName.Trim();
        var account = await identityService.ProvisionAccountAsync(fullName, email, phoneNumber, role: null,
            cancellationToken: cancellationToken);
        if (account.IsFailure) return account.Error!;

        if (group.CompanyId is { } groupCompanyId)
        {
            var company = await context.Companies.FirstAsync(c => c.Id == groupCompanyId, cancellationToken);
            var alreadyPilgrim = await context.Groups.Where(g => g.CompanyId == groupCompanyId)
                .SelectMany(g => g.Members).AnyAsync(m => m.UserId == account.Value.Id, cancellationToken);
            if (!alreadyPilgrim && company.MaxPilgrims is { } max)
            {
                var pilgrimCount = await context.Groups.Where(g => g.CompanyId == groupCompanyId)
                    .SelectMany(g => g.Members.Where(m => m.Role == GroupMemberRole.Member).Select(m => m.UserId))
                    .Distinct().CountAsync(cancellationToken);
                if (pilgrimCount >= max) return CompanyErrors.LimitReached;
            }
        }

        var add = group.AddMember(account.Value.Id, timeProvider.GetUtcNow(), addedBySupervisorId);
        if (add.IsFailure) return add.Error!;
        await context.SaveChangesAsync(cancellationToken);

        if (account.Value.IsNew)
        {
            var companyName = group.CompanyId is { } groupCompanyIdForEmail
                ? await context.Companies.Where(c => c.Id == groupCompanyIdForEmail).Select(c => c.Name).FirstOrDefaultAsync(cancellationToken)
                : null;
            await emailSender.TrySendAsync(account.Value.Email,
                AccountEmails.For(account.Value, "pilgrim", companyName, appLinks.Value.DownloadUrl), logger, cancellationToken);
        }

        return await group.ToCompanyDtoAsync(userDirectory, cancellationToken);
    }
}
