using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Labaik.Api.IntegrationTests.Infrastructure;

namespace Labaik.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class TripTests(LabaikApiFactory factory)
{
    public static object NewTrip(string name = "December Umrah", string departure = "2030-12-10") => new
    {
        name,
        pilgrimageType = 1,
        destinations = new[] { "Makkah", "Madinah" },
        departureDate = departure,
        returnDate = "2030-12-20",
        departurePlace = "Amman",
        expectedPilgrims = 40,
        notes = (string?)null
    };

    [Fact]
    public async Task Company_creates_lists_reads_and_updates_its_trips()
    {
        var (owner, _) = await TestUsers.CreateCompanyAsync(factory);

        var created = await owner.Client.PostAsJsonAsync("/api/v1/company/trips", NewTrip());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var trip = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = trip.GetProperty("id").GetGuid();
        Assert.Equal(1, trip.GetProperty("status").GetInt32()); // Upcoming
        Assert.Equal("2030-12-10", trip.GetProperty("departureDate").GetString());

        var list = await owner.Client.GetFromJsonAsync<JsonElement>("/api/v1/company/trips");
        Assert.Equal(id, Assert.Single(list.EnumerateArray()).GetProperty("id").GetGuid());

        var update = await owner.Client.PutAsJsonAsync($"/api/v1/company/trips/{id}", NewTrip("Renamed"));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var details = await owner.Client.GetFromJsonAsync<JsonElement>($"/api/v1/company/trips/{id}");
        Assert.Equal("Renamed", details.GetProperty("name").GetString());
        Assert.Equal(2, details.GetProperty("destinations").GetArrayLength());
    }

    [Fact]
    public async Task A_company_cannot_see_another_companys_trip()
    {
        var (owner, _) = await TestUsers.CreateCompanyAsync(factory);
        var (other, _) = await TestUsers.CreateCompanyAsync(factory);
        var id = (await (await owner.Client.PostAsJsonAsync("/api/v1/company/trips", NewTrip()))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.GetAsync($"/api/v1/company/trips/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await other.Client.PutAsJsonAsync($"/api/v1/company/trips/{id}", NewTrip())).StatusCode);
        Assert.Empty((await other.Client.GetFromJsonAsync<JsonElement>("/api/v1/company/trips")).EnumerateArray());
    }

    [Fact]
    public async Task Invalid_trip_is_rejected_and_pilgrims_are_forbidden()
    {
        var (owner, _) = await TestUsers.CreateCompanyAsync(factory);
        var pilgrim = await TestUsers.CreateAsync(factory);

        var invalid = await owner.Client.PostAsJsonAsync("/api/v1/company/trips", new
        {
            name = "", pilgrimageType = 9, destinations = Array.Empty<string>(), departureDate = "2030-12-10",
            returnDate = "2030-12-01", departurePlace = "", expectedPilgrims = 0
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
        var errors = (await invalid.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("Name", out _) || errors.TryGetProperty("name", out _));

        Assert.Equal(HttpStatusCode.Forbidden, (await pilgrim.Client.PostAsJsonAsync("/api/v1/company/trips", NewTrip())).StatusCode);
    }
}
