using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Map.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Map;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Map.Queries.GetMapPlaces;

// Published field-mode places; type omitted = all, ordered by type then seed order.
public sealed record GetMapPlacesQuery(MapPlaceType? Type) : IRequest<Result<IReadOnlyList<MapPlaceDto>>>;

public sealed class GetMapPlacesQueryValidator : AbstractValidator<GetMapPlacesQuery>
{
    public GetMapPlacesQueryValidator() => RuleFor(x => x.Type).IsInEnum();
}

public sealed class GetMapPlacesQueryHandler(IAppDbContext context)
    : IRequestHandler<GetMapPlacesQuery, Result<IReadOnlyList<MapPlaceDto>>>
{
    public async Task<Result<IReadOnlyList<MapPlaceDto>>> Handle(GetMapPlacesQuery request, CancellationToken cancellationToken)
    {
        var places = await context.MapPlaces
            .AsNoTracking()
            .Where(p => p.IsPublished && (request.Type == null || p.Type == request.Type))
            .OrderBy(p => p.Type)
            .ThenBy(p => p.SortOrder)
            .ToListAsync(cancellationToken);

        return places.Select(p => p.ToDto()).ToList();
    }
}
