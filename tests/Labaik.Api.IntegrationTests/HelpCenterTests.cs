using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Labaik.Api.IntegrationTests.Infrastructure;

namespace Labaik.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class HelpCenterTests(LabaikApiFactory factory)
{
    private async Task<(TestUser Supervisor, TestUser Pilgrim, Guid GroupId)> SupervisedPilgrimAsync()
    {
        var (company, _) = await TestUsers.CreateCompanyAsync(factory);
        var supervisor = await TestUsers.CreateAsync(factory, name: "Help Supervisor");
        var pilgrim = await TestUsers.CreateAsync(factory, name: "Help Pilgrim");
        await TestUsers.AddToRosterAsync(company, supervisor);
        var groupId = await CompanyGroupTests.CreateTripGroupAsync(company, "Help Group");
        await company.Client.PutAsJsonAsync($"/api/v1/company/groups/{groupId}/supervisor", new { email = supervisor.Email });
        await company.Client.PostAsJsonAsync($"/api/v1/company/groups/{groupId}/members", new { email = pilgrim.Email });
        return (supervisor, pilgrim, groupId);
    }

    private static Task<List<JsonElement>> NotificationsAsync(TestUser user, int type) =>
        user.Client.GetFromJsonAsync<JsonElement>("/api/v1/notifications")
            .ContinueWith(t => t.Result.EnumerateArray().Where(n => n.GetProperty("type").GetInt32() == type).ToList());

    [Fact]
    public async Task Pilgrim_gets_the_supervisor_contact_or_204_without_one()
    {
        var (supervisor, pilgrim, groupId) = await SupervisedPilgrimAsync();
        var loner = await TestUsers.CreateAsync(factory);

        var contact = await pilgrim.Client.GetFromJsonAsync<JsonElement>("/api/v1/pilgrim/supervisor");
        Assert.Equal(supervisor.Id, contact.GetProperty("supervisorId").GetGuid());
        Assert.Equal("Help Supervisor", contact.GetProperty("fullName").GetString());
        Assert.Equal(supervisor.Email, contact.GetProperty("email").GetString());
        Assert.Equal(groupId, contact.GetProperty("groupId").GetGuid());

        Assert.Equal(HttpStatusCode.NoContent, (await loner.Client.GetAsync("/api/v1/pilgrim/supervisor")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await loner.Client.GetAsync($"/api/v1/pilgrim/supervisor?groupId={groupId}")).StatusCode);
    }

    [Fact]
    public async Task Lost_child_report_alerts_supervisor_and_nearby_sharers_only()
    {
        var (supervisor, pilgrim, _) = await SupervisedPilgrimAsync();
        var nearby = await TestUsers.CreateAsync(factory);
        var far = await TestUsers.CreateAsync(factory);
        async Task ShareAsync(TestUser u, double lat, double lng)
        {
            await u.Client.PutAsJsonAsync("/api/v1/location/sharing", new { isSharing = true });
            await u.Client.PostAsJsonAsync("/api/v1/location", new { latitude = lat, longitude = lng });
        }
        await ShareAsync(nearby, 21.4230, 39.8265); // ~60 m away
        await ShareAsync(far, 21.4500, 39.8265);    // ~3 km away

        var created = await pilgrim.Client.PostAsJsonAsync("/api/v1/lost-child-reports", new
        {
            approxAge = 6,
            description = "Boy, red shirt, answers to Omar",
            photoUrl = "https://cdn.example.com/child.jpg",
            latitude = 21.4225,
            longitude = 39.8265,
            locationDescription = "Near gate 79",
            shareWithNearby = true
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var reportId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        Assert.Equal(reportId, Assert.Single(await NotificationsAsync(supervisor, 11)).GetProperty("referenceId").GetGuid());
        Assert.Single(await NotificationsAsync(nearby, 11));
        Assert.Empty(await NotificationsAsync(far, 11));
        Assert.Empty(await NotificationsAsync(pilgrim, 11));

        Assert.Equal(HttpStatusCode.OK, (await nearby.Client.GetAsync($"/api/v1/lost-child-reports/{reportId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await far.Client.GetAsync($"/api/v1/lost-child-reports/{reportId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await nearby.Client.PostAsync($"/api/v1/lost-child-reports/{reportId}/found", null)).StatusCode);

        var found = await (await pilgrim.Client.PostAsync($"/api/v1/lost-child-reports/{reportId}/found", null))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, found.GetProperty("status").GetInt32());
        var mine = await pilgrim.Client.GetFromJsonAsync<JsonElement>("/api/v1/lost-child-reports/mine");
        Assert.Equal(reportId, Assert.Single(mine.EnumerateArray()).GetProperty("id").GetGuid());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await pilgrim.Client.PostAsJsonAsync("/api/v1/lost-child-reports",
            new { approxAge = 30, description = "", latitude = 21.42, longitude = 39.82 })).StatusCode);
    }
}
