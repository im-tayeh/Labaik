using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Labaik.Api.IntegrationTests.Infrastructure;

namespace Labaik.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class JourneyTests(LabaikApiFactory factory)
{
    private static object NewJourney() => new
    {
        pilgrimageType = 1,
        isFirstTime = true,
        travelParty = 1,
        arrival = new { mode = 1, entryPoint = "Jeddah" },
        stays = new[] { new { cityName = "Makkah", accommodationType = 1, placeName = "Hotel A", arrivalAtUtc = "2026-11-01T10:00:00Z" } }
    };

    private static object Edit(string placeName, params string[] arrivals) => new
    {
        isFirstTime = false,
        travelParty = 2,
        arrival = new { mode = 2, entryPoint = "Madinah" },
        stays = arrivals.Select(a => new { cityName = "Makkah", accommodationType = 1, placeName, arrivalAtUtc = a }).ToArray()
    };

    private static async Task<Guid> CreateJourneyAsync(TestUser user)
    {
        var response = await user.Client.PostAsJsonAsync("/api/v1/journeys/", NewJourney());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Onboarding_creates_a_ready_journey()
    {
        var user = await TestUsers.CreateAsync(factory);
        var id = await CreateJourneyAsync(user);

        var journey = await user.Client.GetFromJsonAsync<JsonElement>($"/api/v1/journeys/{id}");

        Assert.Equal(2, journey.GetProperty("status").GetInt32()); // Ready
        Assert.Single(journey.GetProperty("stays").EnumerateArray());
    }

    [Fact]
    public async Task Editing_twice_replaces_details_and_stays()
    {
        var user = await TestUsers.CreateAsync(factory);
        var id = await CreateJourneyAsync(user);

        var first = await user.Client.PutAsJsonAsync($"/api/v1/journeys/{id}",
            Edit("Hotel B", "2026-11-05T10:00:00Z", "2026-11-02T10:00:00Z"));
        var second = await user.Client.PutAsJsonAsync($"/api/v1/journeys/{id}", Edit("Hotel C", "2026-11-03T10:00:00Z"));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var journey = await user.Client.GetFromJsonAsync<JsonElement>("/api/v1/journeys/me");
        Assert.Equal(2, journey.GetProperty("travelParty").GetInt32());
        Assert.Equal("Hotel C", Assert.Single(journey.GetProperty("stays").EnumerateArray()).GetProperty("placeName").GetString());
    }

    [Fact]
    public async Task Another_users_journey_is_not_found()
    {
        var owner = await TestUsers.CreateAsync(factory);
        var other = await TestUsers.CreateAsync(factory);
        var id = await CreateJourneyAsync(owner);

        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.GetAsync($"/api/v1/journeys/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await other.Client.PutAsJsonAsync($"/api/v1/journeys/{id}", Edit("X", "2026-11-03T10:00:00Z"))).StatusCode);
    }

    [Fact]
    public async Task Editing_with_no_stays_is_rejected()
    {
        var user = await TestUsers.CreateAsync(factory);
        var id = await CreateJourneyAsync(user);

        var response = await user.Client.PutAsJsonAsync($"/api/v1/journeys/{id}", Edit("X"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
}
