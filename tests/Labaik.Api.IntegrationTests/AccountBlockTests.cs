using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Labaik.Api.IntegrationTests.Infrastructure;

namespace Labaik.Api.IntegrationTests;

// Tokens issued before a block stop working on the next request, not when they expire.
[Collection(ApiCollection.Name)]
public class AccountBlockTests(LabaikApiFactory factory)
{
    [Fact]
    public async Task Stopping_a_company_blocks_its_signed_in_owner_and_supervisor_until_resumed()
    {
        var admin = await TestUsers.GetAdminAsync(factory);
        var (company, companyId) = await TestUsers.CreateCompanyAsync(factory);
        var supervisor = await TestUsers.CreateAsync(factory);
        await TestUsers.AddToRosterAsync(company, supervisor);
        supervisor = await TestUsers.RefreshAsync(factory, supervisor);
        Assert.Equal(HttpStatusCode.OK, (await company.Client.GetAsync("/api/v1/company/trips")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await supervisor.Client.GetAsync("/api/v1/supervisor/groups")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await admin.Client.PostAsync($"/api/v1/admin/companies/{companyId}/stop", null)).StatusCode);

        await AssertBlockedAsync(company.Client.GetAsync("/api/v1/company/trips"), "Company.Stopped");
        await AssertBlockedAsync(supervisor.Client.GetAsync("/api/v1/supervisor/groups"), "Company.Stopped");
        Assert.Equal(HttpStatusCode.NoContent, (await company.Client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = company.RefreshToken })).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await admin.Client.PostAsync($"/api/v1/admin/companies/{companyId}/resume", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await supervisor.Client.GetAsync("/api/v1/supervisor/groups")).StatusCode);
    }

    [Fact]
    public async Task Suspending_a_supervisor_blocks_the_access_token_they_already_hold()
    {
        var (company, _) = await TestUsers.CreateCompanyAsync(factory);
        var supervisor = await TestUsers.CreateAsync(factory);
        await TestUsers.AddToRosterAsync(company, supervisor);
        supervisor = await TestUsers.RefreshAsync(factory, supervisor);
        Assert.Equal(HttpStatusCode.OK, (await supervisor.Client.GetAsync("/api/v1/supervisor/groups")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await company.Client.PostAsync($"/api/v1/company/supervisors/{supervisor.Id}/disable", null)).StatusCode);
        await AssertBlockedAsync(supervisor.Client.GetAsync("/api/v1/supervisor/groups"), "Supervisor.Suspended");
        await AssertBlockedAsync(supervisor.Client.GetAsync("/api/v1/notifications"), "Supervisor.Suspended");

        Assert.Equal(HttpStatusCode.NoContent, (await company.Client.PostAsync($"/api/v1/company/supervisors/{supervisor.Id}/enable", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await supervisor.Client.GetAsync("/api/v1/supervisor/groups")).StatusCode);
    }

    [Fact]
    public async Task Disabling_an_account_blocks_its_access_token()
    {
        var admin = await TestUsers.GetAdminAsync(factory);
        var pilgrim = await TestUsers.CreateAsync(factory);
        Assert.Equal(HttpStatusCode.OK, (await pilgrim.Client.GetAsync("/api/v1/notifications")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.Client.PostAsync($"/api/v1/admin/users/{pilgrim.Id}/disable", null)).StatusCode);
        await AssertBlockedAsync(pilgrim.Client.GetAsync("/api/v1/notifications"), "Auth.AccountDisabled");
    }

    private static async Task AssertBlockedAsync(Task<HttpResponseMessage> request, string code)
    {
        var response = await request;
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }
}
