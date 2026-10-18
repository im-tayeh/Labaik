using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Labaik.Api.IntegrationTests.Infrastructure;

namespace Labaik.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class CompanyTests(LabaikApiFactory factory)
{
    [Fact]
    public async Task Admin_creates_a_company_and_the_owner_gets_the_Company_role()
    {
        var (owner, companyId) = await TestUsers.CreateCompanyAsync(factory, "Al Noor Travel");

        var me = await owner.Client.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");
        var company = await owner.Client.GetFromJsonAsync<JsonElement>("/api/v1/company/me");

        Assert.Contains("Company", me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
        Assert.Equal(companyId, company.GetProperty("id").GetGuid());
        Assert.Equal("Al Noor Travel", company.GetProperty("name").GetString());
    }

    [Fact]
    public async Task An_account_can_own_only_one_company()
    {
        var admin = await TestUsers.GetAdminAsync(factory);
        var (owner, _) = await TestUsers.CreateCompanyAsync(factory);

        var again = await admin.Client.PostAsJsonAsync("/api/v1/admin/companies", TestUsers.NewCompanyBody("Second", owner.Email));

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Creating_a_company_requires_a_registered_owner_and_an_admin()
    {
        var admin = await TestUsers.GetAdminAsync(factory);
        var pilgrim = await TestUsers.CreateAsync(factory);
        var body = TestUsers.NewCompanyBody("X Travel", $"nobody-{Guid.NewGuid():N}@labaik.test");

        Assert.Equal(HttpStatusCode.NotFound, (await admin.Client.PostAsJsonAsync("/api/v1/admin/companies", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await pilgrim.Client.PostAsJsonAsync("/api/v1/admin/companies", body)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await admin.Client.PostAsJsonAsync("/api/v1/admin/companies", new { name = "", ownerEmail = "bad" })).StatusCode);
    }

    [Fact]
    public async Task Company_endpoints_are_for_company_accounts_only()
    {
        var pilgrim = await TestUsers.CreateAsync(factory);

        Assert.Equal(HttpStatusCode.Forbidden, (await pilgrim.Client.GetAsync("/api/v1/company/me")).StatusCode);
    }
}
