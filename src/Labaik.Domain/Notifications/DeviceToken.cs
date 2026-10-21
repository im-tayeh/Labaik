using Labaik.Domain.Common;
using Labaik.Domain.Common.Results;

namespace Labaik.Domain.Notifications;

public enum DevicePlatform { Android = 1, Ios = 2 }

public sealed class DeviceToken : Entity
{
    public const int TokenMaxLength = 4096;
    public Guid UserId { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public DevicePlatform Platform { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private DeviceToken() { }

    private DeviceToken(Guid userId, string token, DevicePlatform platform, DateTimeOffset nowUtc)
    { UserId = userId; Token = token; Platform = platform; UpdatedAtUtc = nowUtc; }

    public static Result<DeviceToken> Create(Guid userId, string token, DevicePlatform platform, DateTimeOffset nowUtc)
    {
        if (userId == Guid.Empty) return Error.Validation("Notification.DeviceUserRequired", "A device owner is required.");
        if (string.IsNullOrWhiteSpace(token)) return Error.Validation("Notification.DeviceTokenRequired", "A device token is required.");
        token = token.Trim();
        if (token.Length > TokenMaxLength) return Error.Validation("Notification.DeviceTokenTooLong", "Device tokens must be at most 4096 characters.");
        if (!Enum.IsDefined(platform)) return Error.Validation("Notification.InvalidDevicePlatform", "Platform must be Android (1) or iOS (2).");
        return new DeviceToken(userId, token, platform, nowUtc);
    }

    public Result Update(Guid userId, DevicePlatform platform, DateTimeOffset nowUtc)
    {
        if (userId == Guid.Empty) return Result.Failure(Error.Validation("Notification.DeviceUserRequired", "A device owner is required."));
        if (!Enum.IsDefined(platform)) return Result.Failure(Error.Validation("Notification.InvalidDevicePlatform", "Platform must be Android (1) or iOS (2)."));
        UserId = userId; Platform = platform; UpdatedAtUtc = nowUtc;
        return Result.Success();
    }
}
