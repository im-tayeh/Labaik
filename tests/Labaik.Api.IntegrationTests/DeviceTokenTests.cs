using System.Net;
using System.Net.Http.Json;
using Labaik.Api.IntegrationTests.Infrastructure;

namespace Labaik.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class DeviceTokenTests(LabaikApiFactory factory)
{
    [Fact]
    public async Task Device_tokens_are_upserted_removed_and_cleared_on_logout()
    {
        var user = await TestUsers.CreateAsync(factory);
        var token = $"fcm-{Guid.NewGuid():N}";
        var request = new { token, platform = 1 };

        Assert.Equal(HttpStatusCode.NoContent,
            (await user.Client.PutAsJsonAsync("/api/v1/notifications/devices", request)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await user.Client.PutAsJsonAsync("/api/v1/notifications/devices", request)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await user.Client.DeleteAsync($"/api/v1/notifications/devices/{token}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await user.Client.PutAsJsonAsync("/api/v1/notifications/devices", request)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await user.Client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = user.RefreshToken, deviceToken = token })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await user.Client.PutAsJsonAsync("/api/v1/notifications/devices", new { token, platform = 9 })).StatusCode);
    }
}
