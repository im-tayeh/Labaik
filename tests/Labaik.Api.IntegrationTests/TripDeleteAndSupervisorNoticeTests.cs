using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Labaik.Api.IntegrationTests.Infrastructure;

namespace Labaik.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class TripDeleteAndSupervisorNoticeTests(LabaikApiFactory factory)
{
    [Fact]
    public async Task Company_deletes_a_trip_with_empty_groups_but_not_one_with_pilgrims()
    {
        var (company, _) = await TestUsers.CreateCompanyAsync(factory);
        var other = (await TestUsers.CreateCompanyAsync(factory, "Other Company")).Owner;

        // A trip with an empty group (and a message in its chat from nobody yet) can go.
        var trip = await company.Client.PostAsJsonAsync("/api/v1/company/trips", TripTests.NewTrip());
        var tripId = (await trip.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Created, (await company.Client.PostAsJsonAsync($"/api/v1/company/trips/{tripId}/groups", new { name = "Empty" })).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.DeleteAsync($"/api/v1/company/trips/{tripId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await company.Client.DeleteAsync($"/api/v1/company/trips/{tripId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await company.Client.GetAsync($"/api/v1/company/trips/{tripId}")).StatusCode);
        var groups = await company.Client.GetFromJsonAsync<JsonElement>("/api/v1/company/groups");
        Assert.Equal(0, groups.GetArrayLength());

        // A trip whose group has a pilgrim is refused.
        var groupId = await CompanyGroupTests.CreateTripGroupAsync(company);
        var pilgrim = await TestUsers.CreateAsync(factory);
        Assert.Equal(HttpStatusCode.OK, (await company.Client.PostAsJsonAsync($"/api/v1/company/groups/{groupId}/members", new { email = pilgrim.Email })).StatusCode);
        var busyTrip = (await company.Client.GetFromJsonAsync<JsonElement>("/api/v1/company/groups"))[0].GetProperty("tripId").GetGuid();
        var refused = await company.Client.DeleteAsync($"/api/v1/company/trips/{busyTrip}");
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("Trip.HasPilgrims", (await refused.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Suspending_and_reactivating_a_supervisor_notifies_the_company_and_the_supervisor()
    {
        var (company, _) = await TestUsers.CreateCompanyAsync(factory, "Notice Company");
        var supervisor = await TestUsers.CreateAsync(factory, name: "Notice Supervisor");
        await TestUsers.AddToRosterAsync(company, supervisor);

        Assert.Equal(HttpStatusCode.NoContent, (await company.Client.PostAsync($"/api/v1/company/supervisors/{supervisor.Id}/disable", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await company.Client.PostAsync($"/api/v1/company/supervisors/{supervisor.Id}/enable", null)).StatusCode);

        var owner = (await company.Client.GetFromJsonAsync<JsonElement>("/api/v1/notifications")).EnumerateArray()
            .Where(n => n.GetProperty("type").GetInt32() is 16 or 17).ToList();
        Assert.Equal([17, 16], owner.Select(n => n.GetProperty("type").GetInt32()));
        Assert.All(owner, n => Assert.Equal("Notice Supervisor", n.GetProperty("preview").GetString()));

        // Reactivated: the supervisor signs in again and sees both notices with the company name.
        var signedIn = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email = supervisor.Email, password = TestUsers.Password });
        var again = await TestUsers.ToUserAsync(factory, supervisor.Email, signedIn);
        var mine = (await again.Client.GetFromJsonAsync<JsonElement>("/api/v1/notifications")).EnumerateArray()
            .Where(n => n.GetProperty("type").GetInt32() is 16 or 17).ToList();
        Assert.Equal(2, mine.Count);
        Assert.All(mine, n => Assert.Equal("Notice Company", n.GetProperty("preview").GetString()));
    }
}
