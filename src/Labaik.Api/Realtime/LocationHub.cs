using Labaik.Application.Features.FamilyCircles.Queries.GetFamilyCircles;
using Labaik.Application.Features.Locations.Commands.UpdateMyLocation;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace Labaik.Api.Realtime;

// Live locations of group members and family circle members.
// Client -> server: UpdateLocation(latitude, longitude, accuracyMeters?)
// Server -> client: "LocationUpdated" (LiveLocationDto), "StoppedSharing" ({ userId })
// Subscriptions (groups and circles) are taken at connect time: reconnect after joining one.
public sealed class LocationHub(ISender sender) : GroupSubscribedHub(sender)
{
    public const string Path = "/hubs/location";

    public static string CircleName(Guid circleId) => $"circle:{circleId}";

    public override async Task OnConnectedAsync()
    {
        var circles = await Sender.Send(new GetMyFamilyCircleIdsQuery(), Context.ConnectionAborted);
        if (circles.IsSuccess)
        {
            foreach (var circleId in circles.Value)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, CircleName(circleId), Context.ConnectionAborted);
            }
        }

        await base.OnConnectedAsync();
    }

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
