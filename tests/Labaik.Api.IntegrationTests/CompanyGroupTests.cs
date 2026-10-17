using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Labaik.Api.IntegrationTests.Infrastructure;

namespace Labaik.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class CompanyGroupTests(LabaikApiFactory factory)
{
    // Creates a trip and one group under it for the given company account; returns the group id.
    public static async Task<Guid> CreateTripGroupAsync(TestUser company, string groupName = "Group A")
    {
        var trip = await company.Client.PostAsJsonAsync("/api/v1/company/trips", TripTests.NewTrip());
        Assert.Equal(HttpStatusCode.Created, trip.StatusCode);
        var tripId = (await trip.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var group = await company.Client.PostAsJsonAsync($"/api/v1/company/trips/{tripId}/groups", new { name = groupName });
        Assert.Equal(HttpStatusCode.Created, group.StatusCode);
        return (await group.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task A_new_trip_group_has_no_supervisor_and_is_listed_under_its_trip()
    {
        var (company, _) = await TestUsers.CreateCompanyAsync(factory);
        var trip = await (await company.Client.PostAsJsonAsync("/api/v1/company/trips", TripTests.NewTrip()))
            .Content.ReadFromJsonAsync<JsonElement>();
        var tripId = trip.GetProperty("id").GetGuid();

        var created = await company.Client.PostAsJsonAsync($"/api/v1/company/trips/{tripId}/groups", new { name = "Group A" });
        var group = await created.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(JsonValueKind.Null, group.GetProperty("supervisorId").ValueKind);
        Assert.Equal(0, group.GetProperty("pilgrimCount").GetInt32());
        var list = await company.Client.GetFromJsonAsync<JsonElement>($"/api/v1/company/trips/{tripId}/groups");
        Assert.Equal(group.GetProperty("id").GetGuid(), Assert.Single(list.EnumerateArray()).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Company_adds_and_removes_pilgrims()
    {
        var (company, _) = await TestUsers.CreateCompanyAsync(factory);
        var pilgrim = await TestUsers.CreateAsync(factory);
        var groupId = await CreateTripGroupAsync(company);

        var added = await ReadAsync(await company.Client.PostAsJsonAsync(
            $"/api/v1/company/groups/{groupId}/members", new { email = pilgrim.Email }));
        Assert.Equal(1, added.GetProperty("pilgrimCount").GetInt32());

        var home = await pilgrim.Client.GetFromJsonAsync<JsonElement>("/api/v1/home");
        Assert.Equal(groupId, home.GetProperty("group").GetProperty("id").GetGuid());

        Assert.Equal(HttpStatusCode.NoContent,
            (await company.Client.DeleteAsync($"/api/v1/company/groups/{groupId}/members/{pilgrim.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await company.Client.DeleteAsync($"/api/v1/company/groups/{groupId}/members/{pilgrim.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await company.Client.PostAsJsonAsync(
            $"/api/v1/company/groups/{groupId}/members", new { email = $"nobody-{Guid.NewGuid():N}@labaik.test" })).StatusCode);
    }

    [Fact]
    public async Task Assigning_a_supervisor_grants_the_role_and_replacing_removes_the_previous_one()
    {
        var (company, _) = await TestUsers.CreateCompanyAsync(factory);
        var first = await TestUsers.CreateAsync(factory, name: "First Supervisor");
        var second = await TestUsers.CreateAsync(factory, name: "Second Supervisor");
        var groupId = await CreateTripGroupAsync(company);
        var url = $"/api/v1/company/groups/{groupId}/supervisor";

        var assigned = await ReadAsync(await company.Client.PutAsJsonAsync(url, new { email = first.Email }));
        Assert.Equal(first.Id, assigned.GetProperty("supervisorId").GetGuid());
        Assert.Equal("First Supervisor", assigned.GetProperty("supervisorName").GetString());

        var refreshed = await TestUsers.RefreshAsync(factory, first);
        var me = await refreshed.Client.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");
        Assert.Contains("Supervisor", me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));

        Assert.Equal(HttpStatusCode.Conflict, (await company.Client.DeleteAsync(
            $"/api/v1/company/groups/{groupId}/members/{first.Id}")).StatusCode); // supervisor is replaced, not removed

        var replaced = await ReadAsync(await company.Client.PutAsJsonAsync(url, new { email = second.Email }));
        Assert.Equal(second.Id, replaced.GetProperty("supervisorId").GetGuid());
        var firstHome = await first.Client.GetFromJsonAsync<JsonElement>("/api/v1/home");
        Assert.Equal(JsonValueKind.Null, firstHome.GetProperty("group").ValueKind);
    }

    [Fact]
    public async Task Company_home_counts_active_trips_and_distinct_people()
    {
        var (company, _) = await TestUsers.CreateCompanyAsync(factory);
        var supervisor = await TestUsers.CreateAsync(factory);
        var inBoth = await TestUsers.CreateAsync(factory);
        var inOne = await TestUsers.CreateAsync(factory);

        // A finished trip (not active) with no groups.
        var past = await company.Client.PostAsJsonAsync("/api/v1/company/trips", new
        {
            name = "Past", pilgrimageType = 1, destinations = new[] { "Makkah" }, departureDate = "2020-01-01",
            returnDate = "2020-01-10", departurePlace = "Amman", expectedPilgrims = 10
        });
        Assert.Equal(HttpStatusCode.Created, past.StatusCode);

        var groupA = await CreateTripGroupAsync(company, "A");
        var groupB = await CreateTripGroupAsync(company, "B");
        foreach (var g in new[] { groupA, groupB })
        {
            await company.Client.PutAsJsonAsync($"/api/v1/company/groups/{g}/supervisor", new { email = supervisor.Email });
            await company.Client.PostAsJsonAsync($"/api/v1/company/groups/{g}/members", new { email = inBoth.Email });
        }
        await company.Client.PostAsJsonAsync($"/api/v1/company/groups/{groupA}/members", new { email = inOne.Email });

        var home = await company.Client.GetFromJsonAsync<JsonElement>("/api/v1/company/home");

        Assert.Equal(2, home.GetProperty("activeTrips").GetInt32());   // 2 upcoming, the past one excluded
        Assert.Equal(2, home.GetProperty("groupsCount").GetInt32());
        Assert.Equal(1, home.GetProperty("supervisorsCount").GetInt32()); // same supervisor on both groups
        Assert.Equal(2, home.GetProperty("pilgrimsCount").GetInt32());    // inBoth counted once
    }

    [Fact]
    public async Task Company_owner_is_notified_of_group_created_waiting_pilgrims_and_supervisor()
    {
        var (company, _) = await TestUsers.CreateCompanyAsync(factory);
        var supervisor = await TestUsers.CreateAsync(factory, name: "Khaled Supervisor");
        var first = await TestUsers.CreateAsync(factory);
        var second = await TestUsers.CreateAsync(factory);
        var groupId = await CreateTripGroupAsync(company, "Group Waiting");

        await company.Client.PostAsJsonAsync($"/api/v1/company/groups/{groupId}/members", new { email = first.Email });
        await company.Client.PostAsJsonAsync($"/api/v1/company/groups/{groupId}/members", new { email = second.Email });
        await company.Client.PutAsJsonAsync($"/api/v1/company/groups/{groupId}/supervisor", new { email = supervisor.Email });

        var list = (await company.Client.GetFromJsonAsync<JsonElement>("/api/v1/notifications")).EnumerateArray().ToList();
        JsonElement Single(int type) => Assert.Single(list, n => n.GetProperty("type").GetInt32() == type);

        Assert.Equal("Group Waiting", Single(3).GetProperty("preview").GetString());          // GroupCreated
        Assert.Equal("Group Waiting", Single(5).GetProperty("preview").GetString());          // GroupWithoutSupervisor, once
        var activated = Single(4);                                                             // SupervisorActivated
        Assert.Equal("Khaled Supervisor", activated.GetProperty("preview").GetString());
        Assert.Equal(supervisor.Id, activated.GetProperty("referenceId").GetGuid());
        Assert.All(list, n => Assert.Equal(groupId, n.GetProperty("groupId").GetGuid()));
    }

    [Fact]
    public async Task A_company_cannot_manage_another_companys_trip_or_group()
    {
        var (owner, _) = await TestUsers.CreateCompanyAsync(factory);
        var (other, _) = await TestUsers.CreateCompanyAsync(factory);
        var pilgrim = await TestUsers.CreateAsync(factory);
        var groupId = await CreateTripGroupAsync(owner);

        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.PostAsJsonAsync(
            $"/api/v1/company/groups/{groupId}/members", new { email = pilgrim.Email })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.PutAsJsonAsync(
            $"/api/v1/company/groups/{groupId}/supervisor", new { email = pilgrim.Email })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.PostAsJsonAsync(
            $"/api/v1/company/trips/{Guid.NewGuid()}/groups", new { name = "X" })).StatusCode);
    }
}
