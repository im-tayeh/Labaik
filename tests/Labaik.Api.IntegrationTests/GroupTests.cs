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
        await TestUsers.AddToRosterAsync(company, leader);
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
    public async Task Only_admins_create_standalone_groups_and_any_signed_in_user_can_join()
    {
        var pilgrim = await TestUsers.CreateAsync(factory);
        var supervisor = await TestUsers.CreateSupervisorAsync(factory);
        var admin = await TestUsers.GetAdminAsync(factory);

        Assert.Equal(HttpStatusCode.Forbidden, (await pilgrim.Client.PostAsJsonAsync("/api/v1/groups/", new { name = "Mine" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await supervisor.Client.PostAsJsonAsync("/api/v1/groups/", new { name = "Mine" })).StatusCode);

        var created = await admin.Client.PostAsJsonAsync("/api/v1/groups/", new { name = "Admin group" });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var code = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("joinCode").GetString();
        Assert.Equal(HttpStatusCode.OK, (await pilgrim.Client.PostAsync($"/api/v1/groups/{code}/join", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await pilgrim.Client.PostAsync($"/api/v1/groups/{code}/join", null)).StatusCode);
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
    public async Task Supervisor_can_manage_group_pilgrims_and_company_is_notified()
    {
        var s = await SetupAsync();
        var email = $"supervisor-added-{Guid.NewGuid():N}@labaik.test";
        var add = await s.Leader.Client.PostAsJsonAsync($"/api/v1/groups/{s.GroupId}/members",
            new { email, fullName = "Supervisor Added" });
        Assert.Equal(HttpStatusCode.OK, add.StatusCode);
        Assert.NotNull(factory.Emails.TemporaryPasswordFor(email));

        var members = await s.Company.Client.GetFromJsonAsync<JsonElement>($"/api/v1/company/groups/{s.GroupId}/members");
        var added = members.EnumerateArray().Single(m => m.GetProperty("email").GetString() == email);
        var userId = added.GetProperty("userId").GetGuid();
        var companyNotifications = await s.Company.Client.GetFromJsonAsync<JsonElement>("/api/v1/notifications");
        Assert.Contains(companyNotifications.EnumerateArray(), n => n.GetProperty("type").GetInt32() == 13);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await s.Member.Client.DeleteAsync($"/api/v1/groups/{s.GroupId}/members/{userId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await s.Leader.Client.DeleteAsync($"/api/v1/groups/{s.GroupId}/members/{userId}")).StatusCode);
        companyNotifications = await s.Company.Client.GetFromJsonAsync<JsonElement>("/api/v1/notifications");
        Assert.Contains(companyNotifications.EnumerateArray(), n => n.GetProperty("type").GetInt32() == 14);
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
    public async Task Chat_has_location_messages_sender_photo_and_per_member_read_state()
    {
        var s = await SetupAsync();
        var url = $"/api/v1/groups/{s.GroupId}/messages";
        await s.Leader.Client.PutAsJsonAsync("/api/v1/me/profile", new { photoUrl = "https://cdn.example.com/leader.jpg" });

        var location = await s.Leader.Client.PostAsJsonAsync(url, new { kind = 2, latitude = 21.4225, longitude = 39.8262, body = "Gate 79" });
        Assert.Equal(HttpStatusCode.Created, location.StatusCode);
        var sent = await location.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, sent.GetProperty("kind").GetInt32());
        Assert.Equal(21.4225, sent.GetProperty("latitude").GetDouble());
        Assert.True(sent.GetProperty("isRead").GetBoolean()); // own message
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await s.Leader.Client.PostAsJsonAsync(url, new { kind = 2, body = "no coordinates" })).StatusCode);
        await s.Leader.Client.PostAsJsonAsync(url, new { body = "Meet there at 6" });

        async Task<int> UnreadAsync() =>
            (await s.Member.Client.GetFromJsonAsync<JsonElement>($"{url}/unread-count")).GetProperty("count").GetInt32();

        Assert.Equal(2, await UnreadAsync());
        var home = await s.Member.Client.GetFromJsonAsync<JsonElement>("/api/v1/home");
        Assert.Equal(2, home.GetProperty("group").GetProperty("unreadMessages").GetInt32());

        var history = (await s.Member.Client.GetFromJsonAsync<JsonElement>(url)).EnumerateArray().ToList();
        Assert.All(history, m => Assert.False(m.GetProperty("isRead").GetBoolean()));
        Assert.All(history, m => Assert.Equal("https://cdn.example.com/leader.jpg", m.GetProperty("senderPhotoUrl").GetString()));

        Assert.Equal(HttpStatusCode.NoContent, (await s.Member.Client.PostAsync($"{url}/read", null)).StatusCode);
        Assert.Equal(0, await UnreadAsync());
        history = (await s.Member.Client.GetFromJsonAsync<JsonElement>(url)).EnumerateArray().ToList();
        Assert.All(history, m => Assert.True(m.GetProperty("isRead").GetBoolean()));
        Assert.Equal(0, (await s.Leader.Client.GetFromJsonAsync<JsonElement>($"{url}/unread-count")).GetProperty("count").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await s.Outsider.Client.PostAsync($"{url}/read", null)).StatusCode);
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

    [Fact]
    public async Task Supervisor_sets_the_gathering_point_and_members_are_notified()
    {
        var s = await SetupAsync();
        var url = $"/api/v1/groups/{s.GroupId}/gathering-point";

        Assert.Equal(HttpStatusCode.NoContent, (await s.Member.Client.GetAsync(url)).StatusCode); // not set yet
        Assert.Equal(HttpStatusCode.Forbidden,
            (await s.Member.Client.PutAsJsonAsync(url, new { address = "x", latitude = 21.4, longitude = 39.8 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Outsider.Client.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await s.Leader.Client.PutAsJsonAsync(url, new { address = "", latitude = 95, longitude = 39.8 })).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await s.Leader.Client.PutAsJsonAsync(url,
            new { address = "King Abdulaziz Gate", latitude = 21.4225, longitude = 39.8262, gatheringTimeUtc = "2030-12-11T05:00:00Z" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s.Leader.Client.PutAsJsonAsync(url,
            new { address = "Gate 79", latitude = 21.4230, longitude = 39.8270 })).StatusCode); // move it

        var point = await s.Member.Client.GetFromJsonAsync<JsonElement>(url);
        Assert.Equal("Gate 79", point.GetProperty("address").GetString());
        Assert.Equal(JsonValueKind.Null, point.GetProperty("gatheringTimeUtc").ValueKind);

        var memberTypes = (await s.Member.Client.GetFromJsonAsync<JsonElement>("/api/v1/notifications"))
            .EnumerateArray().Where(n => n.GetProperty("type").GetInt32() == 6).ToList();
        Assert.Equal(2, memberTypes.Count);
        Assert.Equal("Gate 79", memberTypes[0].GetProperty("preview").GetString()); // newest first
        var leaderTypes = (await s.Leader.Client.GetFromJsonAsync<JsonElement>("/api/v1/notifications"))
            .EnumerateArray().Count(n => n.GetProperty("type").GetInt32() == 6);
        Assert.Equal(0, leaderTypes);
    }

    [Fact]
    public async Task Attendance_roll_call()
    {
        var s = await SetupAsync();
        var baseUrl = $"/api/v1/groups/{s.GroupId}/attendance";

        Assert.Equal(HttpStatusCode.Forbidden, (await s.Member.Client.GetAsync(baseUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Leader.Client.PostAsync($"{baseUrl}/confirm", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Outsider.Client.PostAsync($"{baseUrl}/confirm", null)).StatusCode);

        var before = await s.Leader.Client.GetFromJsonAsync<JsonElement>(baseUrl);
        Assert.Equal(0, before.GetProperty("presentCount").GetInt32());
        Assert.Equal(1, before.GetProperty("absentCount").GetInt32());

        Assert.Equal(HttpStatusCode.NoContent, (await s.Member.Client.PostAsync($"{baseUrl}/confirm", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await s.Member.Client.PostAsync($"{baseUrl}/confirm", null)).StatusCode);

        var after = await s.Leader.Client.GetFromJsonAsync<JsonElement>(baseUrl);
        var row = Assert.Single(after.GetProperty("members").EnumerateArray());
        Assert.Equal(1, after.GetProperty("presentCount").GetInt32());
        Assert.Equal(1, row.GetProperty("status").GetInt32());
        Assert.Equal("Member", row.GetProperty("fullName").GetString());

        var arrived = (await s.Leader.Client.GetFromJsonAsync<JsonElement>("/api/v1/notifications"))
            .EnumerateArray().Where(n => n.GetProperty("type").GetInt32() == 7).ToList();
        Assert.Equal(s.Member.Id, Assert.Single(arrived).GetProperty("referenceId").GetGuid()); // once

        var manual = await s.Leader.Client.PutAsJsonAsync($"{baseUrl}/{s.Member.Id}", new { status = 2 });
        Assert.Equal(0, (await manual.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("presentCount").GetInt32());
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await s.Leader.Client.PutAsJsonAsync($"{baseUrl}/{s.Member.Id}", new { status = 9 })).StatusCode);

        await s.Member.Client.PostAsync($"{baseUrl}/confirm", null);
        var reset = await s.Leader.Client.PostAsync($"{baseUrl}/reset", null);
        Assert.Equal(0, (await reset.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("presentCount").GetInt32());
    }

    [Fact]
    public async Task Member_card_with_health_notes_is_for_the_supervisor_only()
    {
        var s = await SetupAsync();
        var profile = await s.Member.Client.PutAsJsonAsync("/api/v1/me/profile",
            new { gender = 2, nationality = "jo", photoUrl = "https://cdn.example.com/m.jpg", healthNotes = "Diabetic" });
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        Assert.Equal("JO", (await s.Member.Client.GetFromJsonAsync<JsonElement>("/api/v1/me/profile")).GetProperty("nationality").GetString());

        var notesUrl = $"/api/v1/groups/{s.GroupId}/members/{s.Member.Id}/notes";
        Assert.Equal(HttpStatusCode.NoContent, (await s.Leader.Client.PutAsJsonAsync(notesUrl, new { notes = "Needs a wheelchair" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Member.Client.PutAsJsonAsync(notesUrl, new { notes = "x" })).StatusCode);

        var cardUrl = $"/api/v1/groups/{s.GroupId}/members/{s.Member.Id}";
        var card = await s.Leader.Client.GetFromJsonAsync<JsonElement>(cardUrl);
        Assert.Equal("Diabetic", card.GetProperty("healthNotes").GetString());
        Assert.Equal("Needs a wheelchair", card.GetProperty("notes").GetString());
        Assert.Equal(1, card.GetProperty("memberNumberInGroup").GetInt32());
        Assert.Equal(s.Member.Email, card.GetProperty("email").GetString());
        Assert.Equal(2, card.GetProperty("gender").GetInt32());

        Assert.Equal(HttpStatusCode.Forbidden, (await s.Member.Client.GetAsync(cardUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Outsider.Client.GetAsync(cardUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await s.Member.Client.PutAsJsonAsync("/api/v1/me/profile", new { nationality = "JORDAN" })).StatusCode);
    }

    [Fact]
    public async Task Company_reads_member_cards_and_lists_without_health_notes()
    {
        var s = await SetupAsync();
        await s.Member.Client.PutAsJsonAsync("/api/v1/me/profile", new { gender = 1, nationality = "SA", healthNotes = "Asthma" });
        var (otherCompany, _) = await TestUsers.CreateCompanyAsync(factory);

        var card = await s.Company.Client.GetFromJsonAsync<JsonElement>($"/api/v1/company/groups/{s.GroupId}/members/{s.Member.Id}");
        Assert.Equal("Asthma", card.GetProperty("healthNotes").GetString());
        Assert.Equal(1, card.GetProperty("memberNumberInGroup").GetInt32());
        Assert.Equal(s.Member.Email, card.GetProperty("email").GetString());

        foreach (var (client, url) in new[]
                 {
                     (s.Company.Client, $"/api/v1/company/groups/{s.GroupId}/members"),
                     (s.Leader.Client, $"/api/v1/groups/{s.GroupId}/members")
                 })
        {
            var rows = (await client.GetFromJsonAsync<JsonElement>(url)).EnumerateArray().ToList();
            Assert.Equal(2, rows.Count);
            Assert.Equal(s.Leader.Id, rows[0].GetProperty("userId").GetGuid()); // supervisor first
            Assert.Equal("SA", rows[1].GetProperty("nationality").GetString());
            Assert.All(rows, r => Assert.False(r.TryGetProperty("healthNotes", out _)));
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await s.Member.Client.GetAsync($"/api/v1/groups/{s.GroupId}/members")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await otherCompany.Client.GetAsync($"/api/v1/company/groups/{s.GroupId}/members/{s.Member.Id}")).StatusCode);
    }
}
