using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Journeys.Dtos;
using Labaik.Application.Features.Journeys.Mappers;
using Labaik.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Journeys.Queries.GetMyJourney;

public sealed record GetMyJourneyQuery : IRequest<Result<JourneyDto>>;

public sealed class GetMyJourneyQueryHandler(
    IAppDbContext context,
    ICurrentUser currentUser) : IRequestHandler<GetMyJourneyQuery, Result<JourneyDto>>
{
    public async Task<Result<JourneyDto>> Handle(GetMyJourneyQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Error.Unauthorized("Auth.Required", "Authentication is required.");
        }

        var journey = await context.Journeys
            .Include(j => j.Stays)
            .AsNoTracking()  // read-only: no change tracking needed -> faster
            .Where(j => j.UserId == userId)
            .OrderByDescending(j => j.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return journey is null
            ? Error.NotFound("Journey.NotFound", "No journey found for the current user.")
            : journey.ToDto();
    }
}