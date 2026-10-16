using Labaik.Application.Features.Locations.Commands.UpdateMyLocation;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace Labaik.Api.Realtime;

// Live locations of group members.
// Client -> server: UpdateLocation(latitude, longitude, accuracyMeters?)
// Server -> client: "LocationUpdated" (LiveLocationDto), "StoppedSharing" ({ userId })
public sealed class LocationHub(ISender sender) : GroupSubscribedHub(sender)
{
    public const string Path = "/hubs/location";

    public async Task UpdateLocation(double latitude, double longitude, double? accuracyMeters)
    {
        try
        {
            var result = await Sender.Send(new UpdateMyLocationCommand(latitude, longitude, accuracyMeters), Context.ConnectionAborted);
            if (result.IsFailure)
            {
                throw new HubException($"{result.Error!.Code}: {result.Error.Description}");
            }
        }
        catch (FluentValidation.ValidationException ex)
        {
            throw new HubException("Validation: " + string.Join(" ", ex.Errors.Select(e => e.ErrorMessage)));
        }
    }
}
