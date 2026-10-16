using Labaik.Application.Features.Locations.Queries.GetMyGroupIds;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Labaik.Api.Realtime;

// Base for hubs whose pushes target the user's groups: each connection joins "group:{id}" for every
// group the user belongs to at connect time (reconnect after joining a new group).
[Authorize]
public abstract class GroupSubscribedHub(ISender sender) : Hub
{
    protected ISender Sender { get; } = sender;

    public static string GroupName(Guid groupId) => $"group:{groupId}";

    public override async Task OnConnectedAsync()
    {
        var result = await Sender.Send(new GetMyGroupIdsQuery(), Context.ConnectionAborted);
        if (result.IsSuccess)
        {
            foreach (var groupId in result.Value)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(groupId), Context.ConnectionAborted);
            }
        }

        await base.OnConnectedAsync();
    }
}
