using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Companies.Common;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Trips;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Trips.Commands.DeleteTrip;

// Deletes a trip and its empty groups (with their chat and announcements). A trip whose groups
// still have pilgrims is refused (409 Trip.HasPilgrims) so no pilgrim loses their group.
public sealed record DeleteTripCommand(Guid TripId) : IRequest<Result>;

public sealed class DeleteTripCommandHandler(IAppDbContext context, ICurrentUser currentUser)
    : IRequestHandler<DeleteTripCommand, Result>
{
    public async Task<Result> Handle(DeleteTripCommand request, CancellationToken cancellationToken)
    {
        var companyId = await context.GetMyCompanyIdAsync(currentUser, cancellationToken);
        if (companyId.IsFailure) return Result.Failure(companyId.Error!);

        // Another company's trip is reported as not found (never leak existence).
        var trip = await context.Trips
            .FirstOrDefaultAsync(t => t.Id == request.TripId && t.CompanyId == companyId.Value, cancellationToken);
        if (trip is null) return Result.Failure(TripErrors.NotFound);

        var groups = await context.Groups.Where(g => g.TripId == trip.Id).ToListAsync(cancellationToken);
        if (groups.Any(g => g.PilgrimCount > 0)) return Result.Failure(TripErrors.HasPilgrims);

        var groupIds = groups.Select(g => g.Id).ToList();
        await context.ChatMessages.Where(m => groupIds.Contains(m.GroupId)).ExecuteDeleteAsync(cancellationToken);
        await context.Announcements.Where(a => a.GroupId != null && groupIds.Contains(a.GroupId.Value)).ExecuteDeleteAsync(cancellationToken);
        context.Groups.RemoveRange(groups);
        context.Trips.Remove(trip);
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
