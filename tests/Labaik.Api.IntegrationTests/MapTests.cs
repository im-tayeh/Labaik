using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Labaik.Api.IntegrationTests.Infrastructure;

namespace Labaik.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class MapTests(LabaikApiFactory factory)
{
    [Fact]
    public async Task Destinations_are_seeded_and_filterable_and_route_guides_to_a_place()
    {
        var user = await TestUsers.CreateAsync(factory);

        var all = (await user.Client.GetFromJsonAsync<JsonElement>("/api/v1/map/destinations")).EnumerateArray().ToList();
        var gates = (await user.Client.GetFromJsonAsync<JsonElement>("/api/v1/map/destinations?type=1")).EnumerateArray().ToList();
        var facilities = (await user.Client.GetFromJsonAsync<JsonElement>("/api/v1/map/destinations?type=3")).EnumerateArray().ToList();

        Assert.NotEmpty(gates);
        Assert.All(gates, g => Assert.Equal(1, g.GetProperty("type").GetInt32()));
        Assert.All(facilities, f => Assert.NotEqual(JsonValueKind.Null, f.GetProperty("facilityKind").ValueKind));
        Assert.True(all.Count > gates.Count + facilities.Count); // sub-gates too
        var kingAbdulaziz = gates.Single(g => g.GetProperty("key").GetString() == "gate-king-abdulaziz");
        Assert.Equal(1, kingAbdulaziz.GetProperty("number").GetInt32());

        var route = await user.Client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/map/route?fromLat=21.4190&fromLng=39.8264&destinationId={kingAbdulaziz.GetProperty("id").GetGuid()}");
        Assert.InRange(route.GetProperty("distanceMeters").GetInt32(), 150, 300);
        Assert.Equal("gate-king-abdulaziz", route.GetProperty("destination").GetProperty("key").GetString());
        Assert.True(route.GetProperty("walkingMinutes").GetInt32() >= 1);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await user.Client.GetAsync("/api/v1/map/route?fromLat=21.4&fromLng=39.8")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await user.Client.GetAsync($"/api/v1/map/route?fromLat=21.4&fromLng=39.8&destinationId={Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Emergency_notifies_my_supervisor_who_can_read_and_resolve_it()
    {
        var (company, _) = await TestUsers.CreateCompanyAsync(factory);
        var supervisor = await TestUsers.CreateAsync(factory, name: "SOS Supervisor");
        var pilgrim = await TestUsers.CreateAsync(factory, name: "SOS Pilgrim");
        var stranger = await TestUsers.CreateAsync(factory);
        await TestUsers.AddToRosterAsync(company, supervisor);
        var groupId = await CompanyGroupTests.CreateTripGroupAsync(company);
        await company.Client.PutAsJsonAsync($"/api/v1/company/groups/{groupId}/supervisor", new { email = supervisor.Email });
        await company.Client.PostAsJsonAsync($"/api/v1/company/groups/{groupId}/members", new { email = pilgrim.Email });

        var sos = await pilgrim.Client.PostAsJsonAsync("/api/v1/map/emergency",
            new { latitude = 21.4225, longitude = 39.8262, accuracyMeters = 10, note = "Feeling faint near gate 79" });
        Assert.Equal(HttpStatusCode.Created, sos.StatusCode);
        var alertId = (await sos.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var notification = (await supervisor.Client.GetFromJsonAsync<JsonElement>("/api/v1/notifications"))
            .EnumerateArray().Single(n => n.GetProperty("type").GetInt32() == 10);
        Assert.Equal(alertId, notification.GetProperty("referenceId").GetGuid());
        Assert.Equal("SOS Pilgrim", notification.GetProperty("preview").GetString());

        var seen = await supervisor.Client.GetFromJsonAsync<JsonElement>($"/api/v1/map/emergency/{alertId}");
        Assert.Equal(21.4225, seen.GetProperty("latitude").GetDouble());
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Client.GetAsync($"/api/v1/map/emergency/{alertId}")).StatusCode);

        var resolved = await (await supervisor.Client.PostAsync($"/api/v1/map/emergency/{alertId}/resolve", null))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, resolved.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Map_location_is_the_same_pipeline_as_location()
    {
        var user = await TestUsers.CreateAsync(factory);
        var body = new { latitude = 21.4225, longitude = 39.8262, accuracyMeters = 5 };

        Assert.Equal(HttpStatusCode.Conflict, (await user.Client.PostAsJsonAsync("/api/v1/map/location", body)).StatusCode); // sharing off
        await user.Client.PutAsJsonAsync("/api/v1/location/sharing", new { isSharing = true });
        Assert.Equal(HttpStatusCode.NoContent, (await user.Client.PostAsJsonAsync("/api/v1/map/location", body)).StatusCode);

        var me = await user.Client.GetFromJsonAsync<JsonElement>("/api/v1/location/me");
        Assert.Equal(21.4225, me.GetProperty("latitude").GetDouble());
    }
}
