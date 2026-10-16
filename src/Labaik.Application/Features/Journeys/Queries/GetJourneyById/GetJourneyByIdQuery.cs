using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Journeys.Dtos;
using Labaik.Application.Features.Journeys.Mappers;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Journeys;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Journeys.Queries.GetJourneyById;

public sealed record GetJourneyByIdQuery(Guid JourneyId) : IRequest<Result<JourneyDto>>;

public sealed class GetJourneyByIdQueryHandler(
    IAppDbContext context,
    ICurrentUser currentUser) : IRequestHandler<GetJourneyByIdQuery, Result<JourneyDto>>
{
    public async Task<Result<JourneyDto>> Handle(GetJourneyByIdQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Error.Unauthorized("Auth.Required", "Authentication is required.");
        }

        var journey = await context.Journeys
            .Include(j => j.Stays)
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == request.JourneyId, cancellationToken);

        // Owner only; someone else's journey is reported as not found (never leak existence).
        return journey is null || journey.UserId != userId
            ? JourneyErrors.NotFound
            : journey.ToDto();
    }
}
