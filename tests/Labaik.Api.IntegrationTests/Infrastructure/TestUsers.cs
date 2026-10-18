using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Labaik.Api.IntegrationTests.Infrastructure;

public sealed record TestUser(Guid Id, string Email, string AccessToken, string RefreshToken, HttpClient Client);

public static class TestUsers
{
    public const string Password = "Passw0rd!x";

    // Registers, verifies with the captured OTP, and returns an authenticated client.
    public static async Task<TestUser> CreateAsync(LabaikApiFactory factory, string? email = null, string name = "Test Pilgrim")
    {
        email ??= $"user-{Guid.NewGuid():N}@labaik.test";
        var anonymous = factory.CreateClient();

        var register = await anonymous.PostAsJsonAsync("/api/v1/auth/register", new { fullName = name, email, password = Password });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        var verify = await anonymous.PostAsJsonAsync("/api/v1/auth/verify-email", new { email, code = factory.Emails.LastCodeFor(email) });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);

        return await ToUserAsync(factory, email, verify);
    }

    public static async Task<TestUser> ToUserAsync(LabaikApiFactory factory, string email, HttpResponseMessage tokensResponse)
    {
        var json = await tokensResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = json.GetProperty("accessToken").GetString()!;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return new TestUser(
            json.GetProperty("user").GetProperty("id").GetGuid(),
            email,
            accessToken,
            json.GetProperty("refreshToken").GetString()!,
            client);
    }

    public static async Task<TestUser> RefreshAsync(LabaikApiFactory factory, TestUser user)
    {
        var response = await factory.CreateClient()
            .PostAsJsonAsync("/api/v1/auth/refresh-token", new { refreshToken = user.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ToUserAsync(factory, user.Email, response);
    }

    // A signed-in supervisor: granted by the bootstrap admin, effective after a token refresh.
    public static async Task<TestUser> CreateSupervisorAsync(LabaikApiFactory factory, string name = "Test Supervisor")
    {
        var admin = await GetAdminAsync(factory);
        var user = await CreateAsync(factory, name: name);

        var grant = await admin.Client.PutAsync($"/api/v1/admin/users/{user.Id}/supervisor", null);
        Assert.Equal(HttpStatusCode.OK, grant.StatusCode);

        return await RefreshAsync(factory, user);
    }

    // A signed-in company owner: an admin creates the company for a registered account,
    // effective after a token refresh.
    public static async Task<(TestUser Owner, Guid CompanyId)> CreateCompanyAsync(LabaikApiFactory factory, string companyName = "Test Company")
    {
        var admin = await GetAdminAsync(factory);
        var owner = await CreateAsync(factory, name: "Company Owner");

        var create = await admin.Client.PostAsJsonAsync("/api/v1/admin/companies", NewCompanyBody(companyName, owner.Email));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var companyId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        return (await RefreshAsync(factory, owner), companyId);
    }

    // Signs in a provisioned account with the temporary password from its email.
    public static async Task<TestUser> SignInProvisionedAsync(LabaikApiFactory factory, string email)
    {
        var password = factory.Emails.TemporaryPasswordFor(email)
            ?? throw new InvalidOperationException($"No temporary password was emailed to {email}.");
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return await ToUserAsync(factory, email, login);
    }

    // Puts a registered account on the company's supervisor roster (linked, no new password).
    public static async Task AddToRosterAsync(TestUser company, TestUser supervisor)
    {
        var response = await company.Client.PostAsJsonAsync("/api/v1/company/supervisors",
            new { fullName = "Roster Supervisor", email = supervisor.Email });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // Minimal valid admin "create company" form (unique registration number per call).
    public static object NewCompanyBody(string name, string ownerEmail) => new
    {
        name,
        commercialRegistrationNumber = $"CR-{Guid.NewGuid():N}"[..20],
        contactFullName = "Contact Person",
        ownerEmail
    };

    public static async Task<TestUser> GetAdminAsync(LabaikApiFactory factory)
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email = LabaikApiFactory.AdminEmail, password = Password });

        return login.StatusCode == HttpStatusCode.OK
            ? await ToUserAsync(factory, LabaikApiFactory.AdminEmail, login)
            : await CreateAsync(factory, LabaikApiFactory.AdminEmail, "Test Admin");
    }
}
