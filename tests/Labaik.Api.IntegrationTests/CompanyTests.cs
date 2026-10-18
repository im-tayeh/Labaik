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
    public async Task A_new_owner_email_gets_a_confirmed_Company_account_with_an_emailed_temporary_password()
    {
        var admin = await TestUsers.GetAdminAsync(factory);
        var email = $"owner-{Guid.NewGuid():N}@labaik.test";

        var created = await admin.Client.PostAsJsonAsync("/api/v1/admin/companies", TestUsers.NewCompanyBody("Fresh Travel", email));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var owner = await TestUsers.SignInProvisionedAsync(factory, email); // no OTP step
        var me = await owner.Client.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");
        var company = await owner.Client.GetFromJsonAsync<JsonElement>("/api/v1/company/me");
        Assert.Contains("Company", me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
        Assert.Equal("Fresh Travel", company.GetProperty("name").GetString());
        Assert.Equal("Contact Person", me.GetProperty("name").GetString());
    }

    [Fact]
    public async Task A_registered_owner_is_linked_and_notified_without_a_password()
    {
        var (owner, _) = await TestUsers.CreateCompanyAsync(factory);

        Assert.Null(factory.Emails.TemporaryPasswordFor(owner.Email));
        Assert.Contains("now has company access", factory.Emails.LastBodyFor(owner.Email));
    }

    [Fact]
    public async Task Creating_a_company_is_admin_only_validated_and_registration_numbers_are_unique()
    {
        var admin = await TestUsers.GetAdminAsync(factory);
        var pilgrim = await TestUsers.CreateAsync(factory);
        var body = TestUsers.NewCompanyBody("X Travel", $"nobody-{Guid.NewGuid():N}@labaik.test");

        Assert.Equal(HttpStatusCode.Forbidden, (await pilgrim.Client.PostAsJsonAsync("/api/v1/admin/companies", body)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await admin.Client.PostAsJsonAsync("/api/v1/admin/companies", new { name = "", ownerEmail = "bad" })).StatusCode);

        var full = await admin.Client.PostAsJsonAsync("/api/v1/admin/companies", new
        {
            name = "Full Form Travel",
            commercialRegistrationNumber = $"CR-{Guid.NewGuid():N}"[..20],
            category = 3,
            officialEmail = "info@fullform.test",
            logoUrl = "https://cdn.example.com/logo.png",
            subscriptionPrice = 1500.50m,
            subscriptionPeriod = 4,
            contractStartDate = "2026-01-01",
            contractEndDate = "2099-12-31",
            maxPilgrims = 500,
            contactFullName = "Full Contact",
            enabledServices = new { gpsTracking = true, duaAndRituals = true, instantNotifications = false, chatWithGuides = true, reportsAndStats = false },
            ownerEmail = $"full-{Guid.NewGuid():N}@labaik.test"
        });
        Assert.Equal(HttpStatusCode.Created, full.StatusCode);
        var dto = await full.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, dto.GetProperty("status").GetInt32());
        Assert.True(dto.GetProperty("enabledServices").GetProperty("gpsTracking").GetBoolean());
        Assert.Equal(1500.50m, dto.GetProperty("subscriptionPrice").GetDecimal());

        var duplicate = new
        {
            name = "Copy",
            commercialRegistrationNumber = dto.GetProperty("commercialRegistrationNumber").GetString(),
            contactFullName = "C",
            ownerEmail = $"copy-{Guid.NewGuid():N}@labaik.test"
        };
        Assert.Equal(HttpStatusCode.Conflict, (await admin.Client.PostAsJsonAsync("/api/v1/admin/companies", duplicate)).StatusCode);
    }

    [Fact]
    public async Task Company_endpoints_are_for_company_accounts_only()
    {
        var pilgrim = await TestUsers.CreateAsync(factory);

        Assert.Equal(HttpStatusCode.Forbidden, (await pilgrim.Client.GetAsync("/api/v1/company/me")).StatusCode);
    }
}
