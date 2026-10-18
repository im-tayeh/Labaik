using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Labaik.Api.IntegrationTests.Infrastructure;

namespace Labaik.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AdminCompanyTests(LabaikApiFactory factory)
{
    private static string Today(int addDays = 0) => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(addDays).ToString("yyyy-MM-dd");

    private static object Form(string name, string registration, string? contractEnd = null, string? ownerEmail = null) => new
    {
        name,
        commercialRegistrationNumber = registration,
        contactFullName = "Contact Person",
        contractStartDate = Today(-30),
        contractEndDate = contractEnd,
        ownerEmail = ownerEmail ?? $"owner-{Guid.NewGuid():N}@labaik.test"
    };

    private static string NewRegistration() => $"CR-{Guid.NewGuid():N}"[..20];

    private static async Task<JsonElement> CreateAsync(TestUser admin, object form)
    {
        var created = await admin.Client.PostAsJsonAsync("/api/v1/admin/companies", form);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return await created.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Companies_list_shows_status_days_remaining_and_active_pilgrims()
    {
        var admin = await TestUsers.GetAdminAsync(factory);
        var ownerEmail = $"owner-{Guid.NewGuid():N}@labaik.test";
        var company = await CreateAsync(admin, Form("Listed Travel", NewRegistration(), Today(20), ownerEmail));
        var owner = await TestUsers.SignInProvisionedAsync(factory, ownerEmail);
        var groupId = await CompanyGroupTests.CreateTripGroupAsync(owner);
        var pilgrim = await TestUsers.CreateAsync(factory);
        Assert.Equal(HttpStatusCode.OK, (await owner.Client.PostAsJsonAsync(
            $"/api/v1/company/groups/{groupId}/members", new { email = pilgrim.Email })).StatusCode);

        var list = await admin.Client.GetFromJsonAsync<JsonElement>("/api/v1/admin/companies");
        var row = list.EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == company.GetProperty("id").GetGuid());

        Assert.Equal(1, row.GetProperty("status").GetInt32());
        Assert.Equal(20, row.GetProperty("daysRemaining").GetInt32());
        Assert.Equal(1, row.GetProperty("activePilgrims").GetInt32());
        Assert.Equal(Today(20), row.GetProperty("subscriptionEndUtc").GetString());
    }

    [Fact]
    public async Task Admin_reads_edits_stops_and_resumes_a_company()
    {
        var admin = await TestUsers.GetAdminAsync(factory);
        var company = await CreateAsync(admin, Form("Editable Travel", NewRegistration()));
        var other = await CreateAsync(admin, Form("Other Travel", NewRegistration()));
        var url = $"/api/v1/admin/companies/{company.GetProperty("id").GetGuid()}";

        var details = await admin.Client.GetFromJsonAsync<JsonElement>(url);
        Assert.Equal("Editable Travel", details.GetProperty("name").GetString());
        Assert.Equal(0, details.GetProperty("stats").GetProperty("groupsCount").GetInt32());

        var edit = new
        {
            name = "Edited Travel",
            commercialRegistrationNumber = details.GetProperty("commercialRegistrationNumber").GetString(),
            contactFullName = "New Contact",
            contractEndDate = Today(-1),
            maxPilgrims = 300
        };
        var edited = await (await admin.Client.PutAsJsonAsync(url, edit)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Edited Travel", edited.GetProperty("name").GetString());
        Assert.Equal(3, edited.GetProperty("status").GetInt32()); // Expired
        Assert.Equal(300, edited.GetProperty("maxPilgrims").GetInt32());

        Assert.Equal(HttpStatusCode.Conflict, (await admin.Client.PutAsJsonAsync(url, new
        {
            name = "Edited Travel",
            commercialRegistrationNumber = other.GetProperty("commercialRegistrationNumber").GetString(),
            contactFullName = "New Contact"
        })).StatusCode);

        var stopped = await (await admin.Client.PostAsync($"{url}/stop", null)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, stopped.GetProperty("status").GetInt32());
        var resumed = await (await admin.Client.PostAsync($"{url}/resume", null)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, resumed.GetProperty("status").GetInt32());

        Assert.Equal(HttpStatusCode.NotFound, (await admin.Client.GetAsync($"/api/v1/admin/companies/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Overview_counts_active_and_expiring_subscriptions()
    {
        var admin = await TestUsers.GetAdminAsync(factory);
        var before = await admin.Client.GetFromJsonAsync<JsonElement>("/api/v1/admin/overview");

        await CreateAsync(admin, Form("Expiring Travel", NewRegistration(), Today(10)));
        await CreateAsync(admin, Form("Expired Travel", NewRegistration(), Today(-1)));
        var after = await admin.Client.GetFromJsonAsync<JsonElement>("/api/v1/admin/overview");

        int Delta(string field) => after.GetProperty(field).GetInt32() - before.GetProperty(field).GetInt32();
        Assert.Equal(0, after.GetProperty("pendingRequests").GetInt32());
        Assert.Equal(2, Delta("totalCompanies"));
        Assert.Equal(1, Delta("activeCompanies"));
        Assert.Equal(1, Delta("activeSubscriptions"));
        Assert.Equal(1, Delta("subscriptionsExpiringIn30Days"));
    }

    [Fact]
    public async Task Company_edits_its_own_profile_but_not_admin_fields()
    {
        var (owner, _) = await TestUsers.CreateCompanyAsync(factory, "Self Edit Travel");

        var response = await owner.Client.PutAsJsonAsync("/api/v1/company/me", new
        {
            name = "Self Edited",
            commercialRegistrationNumber = NewRegistration(),
            contactFullName = "Owner Contact",
            logoUrl = "https://cdn.example.com/logo.png",
            country = "Saudi Arabia",
            city = "Jeddah",
            officialEmail = "info@selfedit.test",
            primaryPhone = "+966500000000"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var me = await owner.Client.GetFromJsonAsync<JsonElement>("/api/v1/company/me");
        Assert.Equal("Self Edited", me.GetProperty("name").GetString());
        Assert.Equal("Jeddah", me.GetProperty("city").GetString());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await owner.Client.PutAsJsonAsync("/api/v1/company/me",
            new { name = "X", commercialRegistrationNumber = "1", contactFullName = "Y", logoUrl = "http://insecure" })).StatusCode);
    }
}
