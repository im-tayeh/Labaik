using Labaik.Domain.Common.Results;

namespace Labaik.Domain.Chat;

public static class ChatErrors
{
    public const int BodyMaxLength = 2000;

    public static Error BodyRequired =>
        Error.Validation("Chat.BodyRequired", "Message text is required.");

    public static Error BodyTooLong =>
        Error.Validation("Chat.BodyTooLong", $"Message text must be at most {BodyMaxLength} characters.");

    public static Error InvalidLocation =>
        Error.Validation("Chat.InvalidLocation", "A location message needs a valid latitude and longitude.");

    public static Error InvalidKind =>
        Error.Validation("Chat.InvalidKind", "Unknown message kind.");

    public static Error NotMember =>
        Error.Forbidden("Chat.NotMember", "Only group members can send messages.");
    public static Error Muted => Error.Forbidden("Chat.Muted", "You are muted in this group chat.");
    public static Error SupervisorOnly => Error.Forbidden("Chat.SupervisorOnly", "Only the supervisor can post in this group chat.");
    public static Error MessageNotFound => Error.NotFound("Chat.MessageNotFound", "The message was not found.");
    public static Error InvalidMuteDuration => Error.Validation("Chat.InvalidMuteDuration", "Mute minutes must be between 0 and 10080.");
}
