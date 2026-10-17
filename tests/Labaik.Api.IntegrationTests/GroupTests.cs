using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Labaik.Api.IntegrationTests.Infrastructure;

namespace Labaik.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class GroupTests(LabaikApiFactory factory)
{
    private sealed record GroupSetup(TestUser Company, TestUser Leader, TestUser Member, TestUser Outsider, Guid GroupId);

    // The managed flow: company -> trip -> group -> supervisor (leader) -> member.
    private async Task<GroupSetup> SetupAsync()
    {
        var (company, _) = await TestUsers.CreateCompanyAsync(factory);
        var leader = await TestUsers.CreateAsync(factory, name: "Leader");
        var member = await TestUsers.CreateAsync(factory, name: "Member");
        var outsider = await TestUsers.CreateAsync(factory, name: "Outsider");

        var groupId = await CompanyGroupTests.CreateTripGroupAsync(company);
        Assert.Equal(HttpStatusCode.OK, (await company.Client.PutAsJsonAsync(
            $"/api/v1/company/groups/{groupId}/supervisor", new { email = leader.Email })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await company.Client.PostAsJsonAsync(
            $"/api/v1/company/groups/{groupId}/members", new { email = member.Email })).StatusCode);

        return new GroupSetup(company, leader, member, outsider, groupId);
    }

    [Fact]
    public async Task Pilgrims_and_supervisors_cannot_create_or_join_standalone_groups()
    {
        var pilgrim = await TestUsers.CreateAsync(factory);
        var supervisor = await TestUsers.CreateSupervisorAsync(factory);
        var admin = await TestUsers.GetAdminAsync(factory);

        Assert.Equal(HttpStatusCode.Forbidden, (await pilgrim.Client.PostAsJsonAsync("/api/v1/groups/", new { name = "Mine" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await supervisor.Client.PostAsJsonAsync("/api/v1/groups/", new { name = "Mine" })).StatusCode);

        var created = await admin.Client.PostAsJsonAsync("/api/v1/groups/", new { name = "Admin group" });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var code = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("joinCode").GetString();
        Assert.Equal(HttpStatusCode.Forbidden, (await pilgrim.Client.PostAsync($"/api/v1/groups/{code}/join", null)).StatusCode);
    }

    [Fact]
    public async Task Adding_a_member_twice_conflicts_and_member_count_is_reported()
    {
        var s = await SetupAsync();

        var again = await s.Company.Client.PostAsJsonAsync($"/api/v1/company/groups/{s.GroupId}/members", new { email = s.Member.Email });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        var home = await s.Member.Client.GetFromJsonAsync<JsonElement>("/api/v1/home");
        Assert.Equal(2, home.GetProperty("group").GetProperty("memberCount").GetInt32());
        Assert.Equal(2, home.GetProperty("group").GetProperty("myRole").GetInt32()); // Member
    }

    [Fact]
    public async Task Announcements_are_leader_only_and_notify_members()
    {
        var s = await SetupAsync();
        var url = $"/api/v1/groups/{s.GroupId}/announcements";

        Assert.Equal(HttpStatusCode.Forbidden,
            (await s.Member.Client.PostAsJsonAsync(url, new { title = "x", body = "y" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await s.Outsider.Client.PostAsJsonAsync(url, new { title = "x", body = "y" })).StatusCode);

        var post = await s.Leader.Client.PostAsJsonAsync(url, new { title = "Bus at 6", body = "Lobby, 6 AM." });
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        var announcementId = (await post.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var list = await s.Member.Client.GetFromJsonAsync<JsonElement>(url);
        Assert.False(Assert.Single(list.EnumerateArray()).GetProperty("isRead").GetBoolean());

        var notifications = await s.Member.Client.GetFromJsonAsync<JsonElement>("/api/v1/notifications");
        var notification = Assert.Single(notifications.EnumerateArray(), n => n.GetProperty("type").GetInt32() == 1);
        Assert.Equal("Bus at 6", notification.GetProperty("preview").GetString());

        Assert.Equal(HttpStatusCode.NoContent,
            (await s.Member.Client.PostAsync($"/api/v1/announcements/{announcementId}/read", null)).StatusCode);
        var home = await s.Member.Client.GetFromJsonAsync<JsonElement>("/api/v1/home");
        Assert.Equal(0, home.GetProperty("group").GetProperty("unreadAnnouncements").GetInt32());
    }

    [Fact]
    public async Task Leader_is_notified_when_a_member_joins()
    {
        var s = await SetupAsync();

        var notifications = await s.Leader.Client.GetFromJsonAsync<JsonElement>("/api/v1/notifications");

        var joined = Assert.Single(notifications.EnumerateArray(), n => n.GetProperty("type").GetInt32() == 2);
        Assert.Equal("Member", joined.GetProperty("preview").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await s.Leader.Client.PostAsync("/api/v1/notifications/read-all", null)).StatusCode);
        var count = await s.Leader.Client.GetFromJsonAsync<JsonElement>("/api/v1/notifications/unread-count");
        Assert.Equal(0, count.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task Group_chat_is_members_only_and_pages_newest_first()
    {
        var s = await SetupAsync();
        var url = $"/api/v1/groups/{s.GroupId}/messages";

        Assert.Equal(HttpStatusCode.Created, (await s.Member.Client.PostAsJsonAsync(url, new { body = "first" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await s.Leader.Client.PostAsJsonAsync(url, new { body = "second" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Outsider.Client.PostAsJsonAsync(url, new { body = "hi" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Outsider.Client.GetAsync(url)).StatusCode);

        var page1 = await s.Member.Client.GetFromJsonAsync<JsonElement>($"{url}?take=1");
        var newest = Assert.Single(page1.EnumerateArray());
        Assert.Equal("second", newest.GetProperty("body").GetString());

        var before = Uri.EscapeDataString(newest.GetProperty("sentAtUtc").GetString()!);
        var page2 = await s.Member.Client.GetFromJsonAsync<JsonElement>($"{url}?take=1&before={before}");
        Assert.Equal("first", Assert.Single(page2.EnumerateArray()).GetProperty("body").GetString());
    }

    [Fact]
    public async Task Member_locations_show_positions_only_for_sharers()
    {
        var s = await SetupAsync();

        Assert.Equal(HttpStatusCode.Conflict,
            (await s.Member.Client.PostAsJsonAsync("/api/v1/location", new { latitude = 21.42, longitude = 39.82 })).StatusCode);

        await s.Member.Client.PutAsJsonAsync("/api/v1/location/sharing", new { isSharing = true });
        Assert.Equal(HttpStatusCode.NoContent,
            (await s.Member.Client.PostAsJsonAsync("/api/v1/location", new { latitude = 21.42, longitude = 39.82, accuracyMeters = 5 })).StatusCode);

        var locations = await s.Leader.Client.GetFromJsonAsync<JsonElement>($"/api/v1/groups/{s.GroupId}/locations");
        var member = locations.EnumerateArray().Single(l => l.GetProperty("userId").GetGuid() == s.Member.Id);
        var leader = locations.EnumerateArray().Single(l => l.GetProperty("userId").GetGuid() == s.Leader.Id);
        Assert.Equal(21.42, member.GetProperty("latitude").GetDouble());
        Assert.False(member.GetProperty("isStale").GetBoolean());
        Assert.Equal(JsonValueKind.Null, leader.GetProperty("latitude").ValueKind);

        await s.Member.Client.PutAsJsonAsync("/api/v1/location/sharing", new { isSharing = false });
        var me = await s.Member.Client.GetFromJsonAsync<JsonElement>("/api/v1/location/me");
        Assert.Equal(JsonValueKind.Null, me.GetProperty("latitude").ValueKind);

        Assert.Equal(HttpStatusCode.NotFound, (await s.Outsider.Client.GetAsync($"/api/v1/groups/{s.GroupId}/locations")).StatusCode);
    }
}
