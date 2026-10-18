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
}
