using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Companies.Common;
using Labaik.Application.Features.Trips.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Groups.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Admin.Queries.GetAdminOverview;

// Admin dashboard home numbers (UTC dates). "Active" company = not stopped and contract not ended.
// - PendingRequests: always 0 (no company sign-up requests exist yet).
// - ActiveSubscriptions: active companies that have a contract end date.
// - SubscriptionsExpiringIn30Days: active companies whose contract ends within the next 30 days (today included).
// - ActivePilgrims: distinct pilgrims in company groups whose trip is not over.
// - ActiveCompanies / TotalCompanies: active ones / all.
public sealed record AdminOverviewDto(
    int PendingRequests,
    int ActiveSubscriptions,
    int SubscriptionsExpiringIn30Days,
    int ActivePilgrims,
    int ActiveCompanies,
    int TotalCompanies);

public sealed record GetAdminOverviewQuery : IRequest<Result<AdminOverviewDto>>;

public sealed class GetAdminOverviewQueryHandler(
    IAppDbContext context,
    TimeProvider timeProvider) : IRequestHandler<GetAdminOverviewQuery, Result<AdminOverviewDto>>
{
    public async Task<Result<AdminOverviewDto>> Handle(GetAdminOverviewQuery request, CancellationToken cancellationToken)
    {
        var today = timeProvider.TodayUtc();
        var in30Days = today.AddDays(30);
        var active = context.Companies.Where(c => !c.IsStopped && (c.ContractEndDate == null || c.ContractEndDate >= today));

        var total = await context.Companies.CountAsync(cancellationToken);
        var activeCompanies = await active.CountAsync(cancellationToken);
        var activeSubscriptions = await active.CountAsync(c => c.ContractEndDate != null, cancellationToken);
        var expiringSoon = await active.CountAsync(c => c.ContractEndDate <= in30Days, cancellationToken);

        var activePilgrims = await context.ActiveTripGroups(today)
            .SelectMany(g => g.Members.Where(m => m.Role == GroupMemberRole.Member))
            .Select(m => m.UserId)
            .Distinct()
            .CountAsync(cancellationToken);

        return new AdminOverviewDto(0, activeSubscriptions, expiringSoon, activePilgrims, activeCompanies, total);
    }
}
