using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Labaik.Api.IntegrationTests.Infrastructure;

namespace Labaik.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class FamilyCircleTests(LabaikApiFactory factory)
{
    private const string Base = "/api/v1/family-circles";

    [Fact]
    public async Task Create_preview_join_list_and_owner_is_notified()
    {
        var owner = await TestUsers.CreateAsync(factory, name: "Circle Owner");
        var relative = await TestUsers.CreateAsync(factory, name: "Circle Relative");

        var created = await owner.Client.PostAsJsonAsync(Base, new { name = "Saleh family" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var circle = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = circle.GetProperty("id").GetGuid();
        var code = circle.GetProperty("joinCode").GetString()!;

        var preview = await relative.Client.GetFromJsonAsync<JsonElement>($"{Base}/preview/{code.ToLowerInvariant()}");
        Assert.Equal("Circle Owner", preview.GetProperty("ownerName").GetString());

        var joined = await relative.Client.PostAsJsonAsync($"{Base}/join", new { code });
        Assert.Equal(HttpStatusCode.OK, joined.StatusCode);
        Assert.Equal(2, (await joined.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("myRole").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, (await relative.Client.PostAsJsonAsync($"{Base}/join", new { code })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await relative.Client.PostAsJsonAsync($"{Base}/join", new { code = "ZZZZZZ" })).StatusCode);

        var mine = await relative.Client.GetFromJsonAsync<JsonElement>($"{Base}/mine");
        Assert.Equal(2, Assert.Single(mine.EnumerateArray()).GetProperty("memberCount").GetInt32());

        var notification = (await owner.Client.GetFromJsonAsync<JsonElement>("/api/v1/notifications"))
            .EnumerateArray().Single(n => n.GetProperty("type").GetInt32() == 12);
        Assert.Equal(id, notification.GetProperty("referenceId").GetGuid());
        Assert.Equal("Circle Relative", notification.GetProperty("preview").GetString());
    }

    [Fact]
    public async Task Locations_show_only_members_who_share_in_the_circle()
    {
        var owner = await TestUsers.CreateAsync(factory);
        var relative = await TestUsers.CreateAsync(factory);
        var outsider = await TestUsers.CreateAsync(factory);
        var circle = await (await owner.Client.PostAsJsonAsync(Base, new { name = "Trip family" })).Content.ReadFromJsonAsync<JsonElement>();
        var id = circle.GetProperty("id").GetGuid();
        await relative.Client.PostAsJsonAsync($"{Base}/join", new { code = circle.GetProperty("joinCode").GetString() });

        // Relative shares globally but not yet in the circle -> hidden.
        await relative.Client.PutAsJsonAsync("/api/v1/location/sharing", new { isSharing = true });
        await relative.Client.PostAsJsonAsync("/api/v1/location", new { latitude = 21.4225, longitude = 39.8262 });
        JsonElement RelativeRow(JsonElement rows) => rows.EnumerateArray().Single(r => r.GetProperty("userId").GetGuid() == relative.Id);

        var hidden = RelativeRow(await owner.Client.GetFromJsonAsync<JsonElement>($"{Base}/{id}/locations"));
        Assert.False(hidden.GetProperty("isSharing").GetBoolean());
        Assert.Equal(JsonValueKind.Null, hidden.GetProperty("latitude").ValueKind);

        Assert.Equal(HttpStatusCode.OK, (await relative.Client.PutAsJsonAsync($"{Base}/{id}/location-sharing", new { isSharing = true })).StatusCode);
        var shown = RelativeRow(await owner.Client.GetFromJsonAsync<JsonElement>($"{Base}/{id}/locations"));
        Assert.True(shown.GetProperty("isSharing").GetBoolean());
        Assert.Equal(21.4225, shown.GetProperty("latitude").GetDouble());

        Assert.Equal(HttpStatusCode.NotFound, (await outsider.Client.GetAsync($"{Base}/{id}/locations")).StatusCode);
    }

    [Fact]
    public async Task Owner_removes_members_and_members_can_leave()
    {
        var owner = await TestUsers.CreateAsync(factory);
        var a = await TestUsers.CreateAsync(factory);
        var b = await TestUsers.CreateAsync(factory);
        var circle = await (await owner.Client.PostAsJsonAsync(Base, new { name = "Friends" })).Content.ReadFromJsonAsync<JsonElement>();
        var id = circle.GetProperty("id").GetGuid();
        var code = circle.GetProperty("joinCode").GetString();
        await a.Client.PostAsJsonAsync($"{Base}/join", new { code });
        await b.Client.PostAsJsonAsync($"{Base}/join", new { code });

        Assert.Equal(HttpStatusCode.Forbidden, (await a.Client.DeleteAsync($"{Base}/{id}/members/{b.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await a.Client.DeleteAsync($"{Base}/{id}/members/{a.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.DeleteAsync($"{Base}/{id}/members/{b.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.Client.DeleteAsync($"{Base}/{id}/members/{owner.Id}")).StatusCode);

        var details = await owner.Client.GetFromJsonAsync<JsonElement>($"{Base}/{id}");
        Assert.Single(details.GetProperty("members").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await a.Client.GetAsync($"{Base}/{id}")).StatusCode);
    }
}
