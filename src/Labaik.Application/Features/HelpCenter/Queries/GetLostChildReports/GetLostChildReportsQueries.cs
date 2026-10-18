using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.HelpCenter.Common;
using Labaik.Application.Features.HelpCenter.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.HelpCenter;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.HelpCenter.Queries.GetLostChildReports;

// My own reports, newest first.
public sealed record GetMyLostChildReportsQuery : IRequest<Result<IReadOnlyList<LostChildReportDto>>>;

public sealed class GetMyLostChildReportsQueryHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    IUserDirectory userDirectory) : IRequestHandler<GetMyLostChildReportsQuery, Result<IReadOnlyList<LostChildReportDto>>>
{
    public async Task<Result<IReadOnlyList<LostChildReportDto>>> Handle(
        GetMyLostChildReportsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Error.Unauthorized("Auth.Required", "Authentication is required.");
        }

        var reports = await context.LostChildReports.AsNoTracking()
            .Where(r => r.ReporterId == userId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var name = await userDirectory.GetFullNameAsync(userId, cancellationToken);
        return reports.Select(r => r.ToDto(name)).ToList();
    }
}

// One report: the reporter or an alerted user (opened from notification type 11); others get 404.
public sealed record GetLostChildReportQuery(Guid ReportId) : IRequest<Result<LostChildReportDto>>;

public sealed class GetLostChildReportQueryHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    IUserDirectory userDirectory) : IRequestHandler<GetLostChildReportQuery, Result<LostChildReportDto>>
{
    public async Task<Result<LostChildReportDto>> Handle(GetLostChildReportQuery request, CancellationToken cancellationToken)
    {
        var report = await context.LostChildReports.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.ReportId, cancellationToken);
        if (report is null || !await LostChildAccess.CanSeeAsync(context, currentUser, report, cancellationToken))
        {
            return HelpCenterErrors.NotFound;
        }

        return report.ToDto(await userDirectory.GetFullNameAsync(report.ReporterId, cancellationToken));
    }
}
