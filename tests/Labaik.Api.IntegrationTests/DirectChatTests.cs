using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Labaik.Api.IntegrationTests.Infrastructure;

namespace Labaik.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class DirectChatTests(LabaikApiFactory factory)
{
    [Fact]
    public async Task Pilgrim_and_supervisor_share_one_private_conversation_with_read_state()
    {
        var (company, _) = await TestUsers.CreateCompanyAsync(factory);
        var supervisor = await TestUsers.CreateAsync(factory);
        var pilgrim = await TestUsers.CreateAsync(factory);
        var outsider = await TestUsers.CreateAsync(factory);
        await TestUsers.AddToRosterAsync(company, supervisor);
        var groupId = await CompanyGroupTests.CreateTripGroupAsync(company);
        Assert.Equal(HttpStatusCode.OK, (await company.Client.PutAsJsonAsync(
            $"/api/v1/company/groups/{groupId}/supervisor", new { email = supervisor.Email })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await company.Client.PostAsJsonAsync(
            $"/api/v1/company/groups/{groupId}/members", new { email = pilgrim.Email })).StatusCode);
        supervisor = await TestUsers.RefreshAsync(factory, supervisor);

        var first = await pilgrim.Client.PostAsJsonAsync("/api/v1/chats/direct", new { userId = supervisor.Id });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstDto = await first.Content.ReadFromJsonAsync<JsonElement>();
        var conversationId = firstDto.GetProperty("id").GetGuid();
        var second = await pilgrim.Client.PostAsJsonAsync("/api/v1/chats/direct", new { userId = supervisor.Id });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(conversationId, (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());

        Assert.Equal(HttpStatusCode.Forbidden, (await pilgrim.Client.PostAsJsonAsync(
            "/api/v1/chats/direct", new { userId = outsider.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.Client.GetAsync(
            $"/api/v1/chats/{conversationId}/messages")).StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await supervisor.Client.PostAsJsonAsync(
            $"/api/v1/chats/{conversationId}/messages", new { body = "Meet at gate 4" })).StatusCode);
        var unread = await pilgrim.Client.GetFromJsonAsync<JsonElement>("/api/v1/chats/");
        Assert.Equal(1, Assert.Single(unread.EnumerateArray()).GetProperty("unreadCount").GetInt32());
        Assert.Equal(HttpStatusCode.NoContent, (await pilgrim.Client.PostAsync(
            $"/api/v1/chats/{conversationId}/messages/read", null)).StatusCode);
        unread = await pilgrim.Client.GetFromJsonAsync<JsonElement>("/api/v1/chats/");
        Assert.Equal(0, Assert.Single(unread.EnumerateArray()).GetProperty("unreadCount").GetInt32());
    }
}
