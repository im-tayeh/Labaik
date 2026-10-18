using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.HelpCenter.Common;
using Labaik.Application.Features.HelpCenter.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.HelpCenter;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.HelpCenter.Commands.MarkLostChildFound;

// The reporter closes the report (idempotent). Alerted users get 403; others 404.
public sealed record MarkLostChildFoundCommand(Guid ReportId) : IRequest<Result<LostChildReportDto>>;

public sealed class MarkLostChildFoundCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    IUserDirectory userDirectory,
    TimeProvider timeProvider) : IRequestHandler<MarkLostChildFoundCommand, Result<LostChildReportDto>>
{
    public async Task<Result<LostChildReportDto>> Handle(MarkLostChildFoundCommand request, CancellationToken cancellationToken)
    {
        var report = await context.LostChildReports.FirstOrDefaultAsync(r => r.Id == request.ReportId, cancellationToken);
        if (report is null || !await LostChildAccess.CanSeeAsync(context, currentUser, report, cancellationToken))
        {
            return HelpCenterErrors.NotFound;
        }

        var found = report.MarkFound(currentUser.Id!.Value, timeProvider.GetUtcNow());
        if (found.IsFailure)
        {
            return found.Error!;
        }

        await context.SaveChangesAsync(cancellationToken);
        return report.ToDto(await userDirectory.GetFullNameAsync(report.ReporterId, cancellationToken));
    }
}
