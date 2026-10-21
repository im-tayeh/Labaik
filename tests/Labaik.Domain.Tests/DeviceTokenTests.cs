using Labaik.Domain.Notifications;

namespace Labaik.Domain.Tests;

public class DeviceTokenTests
{
    [Fact]
    public void Device_token_validates_owner_platform_and_length()
    {
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        Assert.True(DeviceToken.Create(userId, " fcm-token ", DevicePlatform.Android, now).IsSuccess);
        Assert.Equal("Notification.DeviceUserRequired", DeviceToken.Create(Guid.Empty, "token", DevicePlatform.Android, now).Error!.Code);
        Assert.Equal("Notification.InvalidDevicePlatform", DeviceToken.Create(userId, "token", (DevicePlatform)3, now).Error!.Code);
        Assert.Equal("Notification.DeviceTokenTooLong", DeviceToken.Create(userId, new string('x', DeviceToken.TokenMaxLength + 1), DevicePlatform.Ios, now).Error!.Code);
    }
}
