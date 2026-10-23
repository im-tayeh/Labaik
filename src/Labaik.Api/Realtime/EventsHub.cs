using MediatR;

namespace Labaik.Api.Realtime;

// Receive-only stream of app events (messages are sent via REST so delivery is acknowledged).
// Server -> client: "MessageReceived" (ChatMessageDto, to the group),
//                   "NotificationReceived" (NotificationDto, to the recipient only),
//                   "AccountBlocked" ({ code }, to the user whose company was stopped, who was suspended or disabled).
public sealed class EventsHub(ISender sender) : GroupSubscribedHub(sender)
{
    public const string Path = "/hubs/events";
}
