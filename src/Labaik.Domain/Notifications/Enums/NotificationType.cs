namespace Labaik.Domain.Notifications.Enums;

// The client renders the (localized) text from the type; the backend stores no user-facing sentences.
// Values are part of the API contract: append new ones with the next number, never renumber.
public enum NotificationType
{
    AnnouncementPosted = 1,     // -> group members (except the author)
    MemberJoinedGroup = 2,      // -> group supervisor
    GroupCreated = 3,           // -> company owner
    SupervisorActivated = 4,    // -> company owner (a supervisor was assigned to a group)
    GroupWithoutSupervisor = 5, // -> company owner (first pilgrim added to a group with no supervisor)
    GatheringPointChanged = 6,  // -> group members except the supervisor (preview = address)
    PilgrimArrived = 7,         // -> group supervisor (preview = pilgrim name)
    GroupMessagePosted = 8,     // -> group members except the sender, when a pilgrim writes in the group chat (preview = text)
    SupervisorMessagePosted = 9, // -> group pilgrims, when the supervisor writes in the group chat (preview = text);
                                 //    reused for 1:1 supervisor messages once direct chat exists
    EmergencyReported = 10      // -> supervisors of the pilgrim's groups (referenceId = alert id, preview = pilgrim name)
}
