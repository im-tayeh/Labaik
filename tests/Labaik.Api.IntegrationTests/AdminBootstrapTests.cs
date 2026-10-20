using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Labaik.Api.IntegrationTests.Infrastructure;
using Labaik.Infrastructure.Persistence.Seed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Labaik.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AdminBootstrapTests(LabaikApiFactory factory)
{
    [Fact]
    public async Task Admin_email_is_seeded_as_a_confirmed_admin_with_an_emailed_temporary_password()
    {
        // No registration: the startup seeder created the account and emailed the password.
        var admin = await TestUsers.SignInProvisionedAsync(factory, LabaikApiFactory.AdminEmail, completeRequiredPasswordChange: false);

        var me = await admin.Client.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");
        Assert.Contains("Admin", me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));

        // Confirmed account: self-registration with the same email is refused.
        var register = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register",
            new { fullName = "Squatter", email = LabaikApiFactory.AdminEmail, password = TestUsers.Password });
        Assert.Equal(HttpStatusCode.Conflict, register.StatusCode);
    }

    [Fact]
    public async Task Reseeding_keeps_the_existing_admin_password()
    {
        var password = factory.Emails.TemporaryPasswordFor(LabaikApiFactory.AdminEmail);

        using (var scope = factory.Services.CreateScope())
        {
            await AdminAccountSeeder.SeedAsync(scope.ServiceProvider, NullLogger.Instance);
        }

        Assert.Equal(password, factory.Emails.TemporaryPasswordFor(LabaikApiFactory.AdminEmail));
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email = LabaikApiFactory.AdminEmail, password });
        if (login.StatusCode == HttpStatusCode.Unauthorized)
        {
            login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
                new { email = LabaikApiFactory.AdminEmail, password = TestUsers.Password });
        }
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }
}
