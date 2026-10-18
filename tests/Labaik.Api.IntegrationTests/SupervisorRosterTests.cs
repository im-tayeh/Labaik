using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Labaik.Api.IntegrationTests.Infrastructure;

namespace Labaik.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class SupervisorRosterTests(LabaikApiFactory factory)
{
    [Fact]
    public async Task Company_provisions_a_supervisor_who_signs_in_with_the_emailed_temporary_password()
    {
        var (company, _) = await TestUsers.CreateCompanyAsync(factory);
        var email = $"sup-{Guid.NewGuid():N}@labaik.test";

        var created = await company.Client.PostAsJsonAsync("/api/v1/company/supervisors",
            new { fullName = "Omar Haddad", email, phoneNumber = "+966551112233" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var dto = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("+966551112233", dto.GetProperty("phoneNumber").GetString());

        var supervisor = await TestUsers.SignInProvisionedAsync(factory, email); // confirmed: no OTP
        var me = await supervisor.Client.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");
        Assert.Contains("Supervisor", me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));

        var roster = await company.Client.GetFromJsonAsync<JsonElement>("/api/v1/company/supervisors");
        var row = Assert.Single(roster.EnumerateArray());
        Assert.Equal("Omar Haddad", row.GetProperty("fullName").GetString());
        Assert.Equal(0, row.GetProperty("groupsCount").GetInt32());

        var home = await company.Client.GetFromJsonAsync<JsonElement>("/api/v1/company/home");
        Assert.Equal(1, home.GetProperty("supervisorsCount").GetInt32()); // counted before any assignment
    }

    [Fact]
    public async Task Roster_rejects_duplicates_and_the_owner_and_is_company_only()
    {
        var (company, _) = await TestUsers.CreateCompanyAsync(factory);
        var pilgrim = await TestUsers.CreateAsync(factory);
        var body = new { fullName = "Dup", email = $"dup-{Guid.NewGuid():N}@labaik.test" };

        Assert.Equal(HttpStatusCode.Created, (await company.Client.PostAsJsonAsync("/api/v1/company/supervisors", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await company.Client.PostAsJsonAsync("/api/v1/company/supervisors", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await company.Client.PostAsJsonAsync("/api/v1/company/supervisors",
            new { fullName = "Me", email = company.Email })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await company.Client.PostAsJsonAsync("/api/v1/company/supervisors",
            new { fullName = "", email = "bad" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await pilgrim.Client.PostAsJsonAsync("/api/v1/company/supervisors", body)).StatusCode);
    }

    [Fact]
    public async Task Only_my_roster_supervisors_can_be_assigned_to_my_groups()
    {
        var (company, _) = await TestUsers.CreateCompanyAsync(factory);
        var (other, _) = await TestUsers.CreateCompanyAsync(factory);
        var notOnRoster = await TestUsers.CreateAsync(factory);
        var othersSupervisor = await TestUsers.CreateAsync(factory);
        await TestUsers.AddToRosterAsync(other, othersSupervisor);
        var groupId = await CompanyGroupTests.CreateTripGroupAsync(company);
        var url = $"/api/v1/company/groups/{groupId}/supervisor";

        Assert.Equal(HttpStatusCode.NotFound, (await company.Client.PutAsJsonAsync(url, new { email = notOnRoster.Email })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await company.Client.PutAsJsonAsync(url, new { email = othersSupervisor.Email })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await company.Client.PutAsJsonAsync(url,
            new { email = $"nobody-{Guid.NewGuid():N}@labaik.test" })).StatusCode);

        await TestUsers.AddToRosterAsync(company, notOnRoster);
        Assert.Equal(HttpStatusCode.OK, (await company.Client.PutAsJsonAsync(url, new { email = notOnRoster.Email })).StatusCode);
        var roster = await company.Client.GetFromJsonAsync<JsonElement>("/api/v1/company/supervisors");
        Assert.Equal(1, Assert.Single(roster.EnumerateArray()).GetProperty("groupsCount").GetInt32());
    }
}
