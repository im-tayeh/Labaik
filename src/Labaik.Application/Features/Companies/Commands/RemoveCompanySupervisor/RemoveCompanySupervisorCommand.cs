using Labaik.Application.Common.Authorization;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Companies.Common;
using Labaik.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Companies.Commands.RemoveCompanySupervisor;

// A company takes a supervisor off its roster. Refused (409) while they still lead one of its groups.
// The account stays; it loses the Supervisor role only when no other company has it on its roster.
public sealed record RemoveCompanySupervisorCommand(Guid UserId) : IRequest<Result>;

public sealed class RemoveCompanySupervisorCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    IIdentityService identityService) : IRequestHandler<RemoveCompanySupervisorCommand, Result>
{
    public async Task<Result> Handle(RemoveCompanySupervisorCommand request, CancellationToken cancellationToken)
    {
        var companyId = await context.GetMyCompanyIdAsync(currentUser, cancellationToken);
        if (companyId.IsFailure)
        {
            return Result.Failure(companyId.Error!);
        }

        var company = await context.Companies.FirstAsync(c => c.Id == companyId.Value, cancellationToken);
        var leadsGroups = await context.Groups
            .AnyAsync(g => g.CompanyId == company.Id && g.LeaderId == request.UserId, cancellationToken);

        var remove = company.RemoveSupervisor(request.UserId, leadsGroups);
        if (remove.IsFailure)
        {
            return remove;
        }

        await context.SaveChangesAsync(cancellationToken);

        var onOtherRoster = await context.Companies
            .AnyAsync(c => c.Id != company.Id && c.Supervisors.Any(s => s.UserId == request.UserId), cancellationToken);
        if (!onOtherRoster)
        {
            await identityService.RemoveFromRoleAsync(request.UserId, Roles.Supervisor, cancellationToken);
        }

        return Result.Success();
    }
}
