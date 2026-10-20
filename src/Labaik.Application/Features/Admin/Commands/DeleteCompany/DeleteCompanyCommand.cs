using Labaik.Application.Common.Interfaces;
using Labaik.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Admin.Commands.DeleteCompany;

public sealed record DeleteCompanyCommand(Guid CompanyId, bool Force) : IRequest<Result>;

public sealed class DeleteCompanyCommandHandler(
    IAppDbContext context, IIdentityService identityService, TimeProvider timeProvider)
    : IRequestHandler<DeleteCompanyCommand, Result>
{
    public async Task<Result> Handle(DeleteCompanyCommand request, CancellationToken cancellationToken)
    {
        var company = await context.Companies.Include(c => c.Supervisors)
            .FirstOrDefaultAsync(c => c.Id == request.CompanyId, cancellationToken);
        if (company is null) return Result.Failure(Error.NotFound("Company.NotFound", "No company found."));

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var hasActiveTrips = await context.Trips.AnyAsync(trip => trip.CompanyId == company.Id
            && trip.DepartureDate <= today && (trip.ReturnDate == null || trip.ReturnDate >= today), cancellationToken);
        if (hasActiveTrips && !request.Force)
            return Result.Failure(Error.Conflict("Company.HasActiveTrips", "The company has trips in progress. Retry with force=true to continue."));

        var ownerDisabled = await identityService.SetDisabledAsync(company.OwnerUserId, true, cancellationToken);
        if (ownerDisabled.IsFailure) return ownerDisabled;
        foreach (var supervisor in company.Supervisors)
        {
            var disabled = await identityService.SetDisabledAsync(supervisor.UserId, true, cancellationToken);
            if (disabled.IsFailure) return disabled;
        }

        company.Delete(timeProvider.GetUtcNow());
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
