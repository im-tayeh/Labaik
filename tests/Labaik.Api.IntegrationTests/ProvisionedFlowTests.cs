using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Labaik.Api.IntegrationTests.Infrastructure;

namespace Labaik.Api.IntegrationTests;

// The demo flow end to end, with only top-down provisioned accounts (no self-registration):
// admin -> company -> supervisor + trip + group -> pilgrim, then announcements, chat and notifications.
[Collection(ApiCollection.Name)]
public class ProvisionedFlowTests(LabaikApiFactory factory)
{
    private static async Task<JsonElement> OkJsonAsync(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        Assert.Equal(expected, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<List<int>> NotificationTypesAsync(TestUser user) =>
        (await user.Client.GetFromJsonAsync<JsonElement>("/api/v1/notifications"))
            .EnumerateArray().Select(n => n.GetProperty("type").GetInt32()).ToList();

    [Fact]
    public async Task Admin_company_supervisor_and_pilgrim_share_announcements_chat_and_notifications()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var ownerEmail = $"owner-{id}@labaik.test";
        var supervisorEmail = $"sup-{id}@labaik.test";
        var pilgrimEmail = $"pilgrim-{id}@labaik.test";

        // Admin provisions the company + owner.
        var admin = await TestUsers.GetAdminAsync(factory);
        await OkJsonAsync(await admin.Client.PostAsJsonAsync("/api/v1/admin/companies",
            TestUsers.NewCompanyBody($"Flow Travel {id}", ownerEmail)), HttpStatusCode.Created);
        var company = await TestUsers.SignInProvisionedAsync(factory, ownerEmail);

        // Company provisions a supervisor, a trip, a group, assigns the supervisor, adds a new pilgrim.
        await OkJsonAsync(await company.Client.PostAsJsonAsync("/api/v1/company/supervisors",
            new { fullName = "Flow Supervisor", email = supervisorEmail }), HttpStatusCode.Created);
        var supervisor = await TestUsers.SignInProvisionedAsync(factory, supervisorEmail);
        var groupId = await CompanyGroupTests.CreateTripGroupAsync(company, "Flow Group");
        await OkJsonAsync(await company.Client.PutAsJsonAsync($"/api/v1/company/groups/{groupId}/supervisor",
            new { email = supervisorEmail }));
        await OkJsonAsync(await company.Client.PostAsJsonAsync($"/api/v1/company/groups/{groupId}/members",
            new { email = pilgrimEmail, fullName = "Flow Pilgrim" }));
        var pilgrim = await TestUsers.SignInProvisionedAsync(factory, pilgrimEmail);

        // Roles in the issued tokens drive the app's screen gating.
        async Task<IEnumerable<string?>> RolesAsync(TestUser u) =>
            (await u.Client.GetFromJsonAsync<JsonElement>("/api/v1/auth/me")).GetProperty("roles").EnumerateArray().Select(r => r.GetString());
        Assert.Contains("Company", await RolesAsync(company));
        Assert.Contains("Supervisor", await RolesAsync(supervisor));
        Assert.Empty(await RolesAsync(pilgrim));

        // Both see the group on their home screen.
        var pilgrimHome = await pilgrim.Client.GetFromJsonAsync<JsonElement>("/api/v1/home");
        Assert.Equal(groupId, pilgrimHome.GetProperty("group").GetProperty("id").GetGuid());
        var supervisorHome = await supervisor.Client.GetFromJsonAsync<JsonElement>("/api/v1/home");
        Assert.Equal(groupId, supervisorHome.GetProperty("group").GetProperty("id").GetGuid());

        // Supervisor announces to the group; company announces to all its pilgrims.
        var groupPost = await OkJsonAsync(await supervisor.Client.PostAsJsonAsync($"/api/v1/groups/{groupId}/announcements",
            new { title = "Gathering", body = "Lobby at 6", importance = 2 }), HttpStatusCode.Created);
        await OkJsonAsync(await company.Client.PostAsJsonAsync("/api/v1/company/announcements",
            new { title = "Welcome", body = "Have a blessed Umrah" }), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.Forbidden, (await pilgrim.Client.PostAsJsonAsync($"/api/v1/groups/{groupId}/announcements",
            new { title = "x", body = "y" })).StatusCode);

        var announcements = (await pilgrim.Client.GetFromJsonAsync<JsonElement>($"/api/v1/groups/{groupId}/announcements"))
            .EnumerateArray().ToList();
        Assert.Equal(2, announcements.Count);
        Assert.All(announcements, a => Assert.False(a.GetProperty("isRead").GetBoolean()));
        Assert.Equal(HttpStatusCode.NoContent,
            (await pilgrim.Client.PostAsync($"/api/v1/announcements/{groupPost.GetProperty("id").GetGuid()}/read", null)).StatusCode);

        // Group chat between pilgrim and supervisor; the owning company reads it (read only).
        await OkJsonAsync(await pilgrim.Client.PostAsJsonAsync($"/api/v1/groups/{groupId}/messages",
            new { body = "Salam, I am at the hotel" }), HttpStatusCode.Created);
        await OkJsonAsync(await supervisor.Client.PostAsJsonAsync($"/api/v1/groups/{groupId}/messages",
            new { body = "Wa alaikum salam, see you at 6" }), HttpStatusCode.Created);
        var messages = (await pilgrim.Client.GetFromJsonAsync<JsonElement>($"/api/v1/groups/{groupId}/messages")).EnumerateArray().ToList();
        Assert.Equal(2, messages.Count);
        Assert.Equal(2, (await company.Client.GetFromJsonAsync<JsonElement>($"/api/v1/groups/{groupId}/messages")).GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await company.Client.PostAsJsonAsync($"/api/v1/groups/{groupId}/messages",
            new { body = "Company cannot post" })).StatusCode);

        // Notifications for each role.
        var pilgrimTypes = await NotificationTypesAsync(pilgrim);
        Assert.Equal(2, pilgrimTypes.Count(t => t == 1));                                       // both announcements
        Assert.Equal(1, pilgrimTypes.Count(t => t == 9));                                       // supervisor's chat message
        Assert.DoesNotContain(8, pilgrimTypes);                                                 // not for own message
        var supervisorTypes = await NotificationTypesAsync(supervisor);
        Assert.Contains(2, supervisorTypes);                                                    // pilgrim joined
        Assert.Contains(8, supervisorTypes);                                                    // pilgrim's chat message
        var companyTypes = await NotificationTypesAsync(company);
        Assert.Contains(3, companyTypes);                                                       // group created
        Assert.Contains(4, companyTypes);                                                       // supervisor activated
        var unread = await pilgrim.Client.GetFromJsonAsync<JsonElement>("/api/v1/notifications/unread-count");
        Assert.Equal(3, unread.GetProperty("count").GetInt32());

        // Dashboards reflect the new accounts.
        var companyHome = await company.Client.GetFromJsonAsync<JsonElement>("/api/v1/company/home");
        Assert.Equal(1, companyHome.GetProperty("supervisorsCount").GetInt32());
        Assert.Equal(1, companyHome.GetProperty("pilgrimsCount").GetInt32());
    }

    [Fact]
    public async Task OpenApi_document_has_examples_for_admin_and_company_endpoints()
    {
        var doc = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        var paths = doc.GetProperty("paths");

        JsonElement Operation(string path, string method) => paths.GetProperty(path).GetProperty(method);
        JsonElement JsonContent(JsonElement holder) => holder.GetProperty("content").GetProperty("application/json");

        var createCompany = Operation("/api/v1/admin/companies", "post");
        Assert.Equal("1010123456", JsonContent(createCompany.GetProperty("requestBody"))
            .GetProperty("example").GetProperty("commercialRegistrationNumber").GetString());
        Assert.True(JsonContent(createCompany.GetProperty("responses").GetProperty("201")).TryGetProperty("example", out _));

        Assert.True(JsonContent(Operation("/api/v1/admin/overview", "get").GetProperty("responses").GetProperty("200"))
            .TryGetProperty("example", out _));
        Assert.True(JsonContent(Operation("/api/v1/company/supervisors", "post").GetProperty("requestBody"))
            .TryGetProperty("example", out _));
    }
}
