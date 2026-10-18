using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Map.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Map;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Map.Queries.GetRoute;

// From my position to a map place (destinationId) or to raw coordinates (toLat + toLng).
public sealed record GetRouteQuery(
    double FromLat,
    double FromLng,
    Guid? DestinationId,
    double? ToLat,
    double? ToLng) : IRequest<Result<MapRouteDto>>;

public sealed class GetRouteQueryValidator : AbstractValidator<GetRouteQuery>
{
    public GetRouteQueryValidator()
    {
        RuleFor(x => x.FromLat).InclusiveBetween(-90, 90);
        RuleFor(x => x.FromLng).InclusiveBetween(-180, 180);
        RuleFor(x => x).Must(x => x.DestinationId is not null || (x.ToLat is not null && x.ToLng is not null))
            .WithName("destination")
            .WithMessage("Give destinationId, or toLat and toLng.");
        RuleFor(x => x.ToLat).InclusiveBetween(-90, 90).When(x => x.ToLat is not null);
        RuleFor(x => x.ToLng).InclusiveBetween(-180, 180).When(x => x.ToLng is not null);
    }
}

public sealed class GetRouteQueryHandler(IAppDbContext context) : IRequestHandler<GetRouteQuery, Result<MapRouteDto>>
{
    private const double WalkingMetersPerSecond = 1.2; // crowded-area walking pace

    public async Task<Result<MapRouteDto>> Handle(GetRouteQuery request, CancellationToken cancellationToken)
    {
        MapPlaceDto? destination = null;
        double toLat, toLng;

        if (request.DestinationId is { } id)
        {
            var place = await context.MapPlaces.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id && p.IsPublished, cancellationToken);
            if (place is null)
            {
                return MapErrors.PlaceNotFound;
            }

            destination = place.ToDto();
            (toLat, toLng) = (place.Latitude, place.Longitude);
        }
        else
        {
            (toLat, toLng) = (request.ToLat!.Value, request.ToLng!.Value);
        }

        var distance = Geo.DistanceMeters(request.FromLat, request.FromLng, toLat, toLng);
        var bearing = Geo.BearingDegrees(request.FromLat, request.FromLng, toLat, toLng);

        return new MapRouteDto(
            new MapPointDto(request.FromLat, request.FromLng),
            new MapPointDto(toLat, toLng),
            destination,
            (int)Math.Round(distance),
            (int)Math.Round(bearing) % 360,
            (int)Math.Ceiling(distance / WalkingMetersPerSecond / 60));
    }
}
