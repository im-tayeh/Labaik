using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Labaik.Api.IntegrationTests.Infrastructure;

namespace Labaik.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class SupervisorRosterTests(LabaikApiFactory factory)
{
    [Fact]
    public async Task Company_cannot_issue_temporary_password_for_another_company_owner_or_admin()
    {
        var (companyA, _) = await TestUsers.CreateCompanyAsync(factory);
        var (companyB, _) = await TestUsers.CreateCompanyAsync(factory);
        var admin = await TestUsers.GetAdminAsync(factory);

        foreach (var protectedUser in new[] { companyB, admin })
        {
            var added = await companyA.Client.PostAsJsonAsync("/api/v1/company/supervisors",
                new { fullName = "Linked Account", email = protectedUser.Email });
            Assert.Equal(HttpStatusCode.Created, added.StatusCode);
            var dto = await added.Content.ReadFromJsonAsync<JsonElement>();
            var userId = dto.GetProperty("userId").GetGuid();

            var response = await companyA.Client.PostAsync($"/api/v1/company/supervisors/{userId}/temp-password", null);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Company.SupervisorNotOwnedByCompany", problem.GetProperty("code").GetString());
        }
    }

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

    [Fact]
    public async Task Company_issues_a_new_temporary_password_that_replaces_the_old_one()
    {
        var (company, _) = await TestUsers.CreateCompanyAsync(factory);
        var email = $"sup-{Guid.NewGuid():N}@labaik.test";
        var created = await company.Client.PostAsJsonAsync("/api/v1/company/supervisors", new { fullName = "Temp Pass", email });
        var userId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("userId").GetGuid();
        var oldPassword = factory.Emails.TemporaryPasswordFor(email)!;

        var issued = await company.Client.PostAsync($"/api/v1/company/supervisors/{userId}/temp-password", null);
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);
        var newPassword = (await issued.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("temporaryPassword").GetString()!;
        Assert.Equal(newPassword, factory.Emails.TemporaryPasswordFor(email)); // emailed too
        Assert.NotEqual(oldPassword, newPassword);

        var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email, password = oldPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email, password = newPassword })).StatusCode);

        var (other, _) = await TestUsers.CreateCompanyAsync(factory);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.PostAsync($"/api/v1/company/supervisors/{userId}/temp-password", null)).StatusCode);
    }

    [Fact]
    public async Task Suspended_supervisor_cannot_sign_in_or_refresh_until_reactivated()
    {
        var (company, _) = await TestUsers.CreateCompanyAsync(factory);
        var email = $"sup-{Guid.NewGuid():N}@labaik.test";
        var created = await company.Client.PostAsJsonAsync("/api/v1/company/supervisors", new { fullName = "Suspend Me", email });
        var userId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("userId").GetGuid();
        var supervisor = await TestUsers.SignInProvisionedAsync(factory, email);
        var password = factory.Emails.TemporaryPasswordFor(email)!;
        var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NoContent, (await company.Client.PostAsync($"/api/v1/company/supervisors/{userId}/disable", null)).StatusCode);
        var roster = await company.Client.GetFromJsonAsync<JsonElement>("/api/v1/company/supervisors");
        Assert.True(Assert.Single(roster.EnumerateArray()).GetProperty("isSuspended").GetBoolean());

        var login = await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
        Assert.Equal("Supervisor.Suspended", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        var refresh = await anonymous.PostAsJsonAsync("/api/v1/auth/refresh-token", new { refreshToken = supervisor.RefreshToken });
        Assert.NotEqual(HttpStatusCode.OK, refresh.StatusCode); // sessions revoked on suspension

        Assert.Equal(HttpStatusCode.NoContent, (await company.Client.PostAsync($"/api/v1/company/supervisors/{userId}/enable", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email, password })).StatusCode);
    }

    [Fact]
    public async Task Company_removes_a_supervisor_only_after_their_groups_are_reassigned()
    {
        var (company, _) = await TestUsers.CreateCompanyAsync(factory);
        var supervisor = await TestUsers.CreateAsync(factory);
        await TestUsers.AddToRosterAsync(company, supervisor);
        var groupId = await CompanyGroupTests.CreateTripGroupAsync(company);
        Assert.Equal(HttpStatusCode.OK, (await company.Client.PutAsJsonAsync($"/api/v1/company/groups/{groupId}/supervisor", new { email = supervisor.Email })).StatusCode);

        var blocked = await company.Client.DeleteAsync($"/api/v1/company/supervisors/{supervisor.Id}");
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal("Company.SupervisorLeadsGroups", (await blocked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        var replacement = await TestUsers.CreateAsync(factory);
        await TestUsers.AddToRosterAsync(company, replacement);
        Assert.Equal(HttpStatusCode.OK, (await company.Client.PutAsJsonAsync($"/api/v1/company/groups/{groupId}/supervisor", new { email = replacement.Email })).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await company.Client.DeleteAsync($"/api/v1/company/supervisors/{supervisor.Id}")).StatusCode);
        var roster = await company.Client.GetFromJsonAsync<JsonElement>("/api/v1/company/supervisors");
        Assert.Equal(replacement.Email, Assert.Single(roster.EnumerateArray()).GetProperty("email").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await company.Client.DeleteAsync($"/api/v1/company/supervisors/{supervisor.Id}")).StatusCode);

        // Off every roster: the Supervisor role is gone from the next token.
        var refreshed = await TestUsers.RefreshAsync(factory, supervisor);
        var me = await refreshed.Client.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");
        Assert.DoesNotContain("Supervisor", me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
    }
}
