using Labaik.Application.Features.Locations.Commands.UpdateMyLocation;
using Labaik.Application.Features.Locations.Queries.GetMyGroupIds;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Labaik.Api.Realtime;

// Live locations of group members.
// Client -> server: UpdateLocation(latitude, longitude, accuracyMeters?)
// Server -> client: "LocationUpdated" (LiveLocationDto), "StoppedSharing" ({ userId })
// A connection is subscribed to the user's groups when it connects; reconnect after joining a new group.
[Authorize]
public sealed class LocationHub(ISender sender) : Hub
{
    public const string Path = "/hubs/location";

    public static string GroupName(Guid groupId) => $"group:{groupId}";

    public override async Task OnConnectedAsync()
    {
        var result = await sender.Send(new GetMyGroupIdsQuery(), Context.ConnectionAborted);
        if (result.IsSuccess)
        {
            foreach (var groupId in result.Value)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(groupId), Context.ConnectionAborted);
            }
        }

        await base.OnConnectedAsync();
    }

    public async Task UpdateLocation(double latitude, double longitude, double? accuracyMeters)
    {
        try
        {
            var result = await sender.Send(new UpdateMyLocationCommand(latitude, longitude, accuracyMeters), Context.ConnectionAborted);
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
