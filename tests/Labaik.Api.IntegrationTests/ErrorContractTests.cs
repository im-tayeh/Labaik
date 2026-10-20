using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Labaik.Api.IntegrationTests.Infrastructure;

namespace Labaik.Api.IntegrationTests;

// The error contract the clients code against: business errors carry the Error's "code";
// 422 keys are camelCase JSON paths matching the request body.
[Collection(ApiCollection.Name)]
public class ErrorContractTests(LabaikApiFactory factory)
{
    private static async Task<JsonElement> ProblemAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Business_errors_carry_the_stable_code_with_status_and_title_unchanged()
    {
        var login = await ProblemAsync(await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email = $"nobody-{Guid.NewGuid():N}@labaik.test", password = "Wrong-pass1" }), HttpStatusCode.Unauthorized);
        Assert.Equal("Auth.InvalidCredentials", login.GetProperty("code").GetString());
        Assert.Equal("Invalid email or password.", login.GetProperty("title").GetString());
        Assert.Equal(401, login.GetProperty("status").GetInt32());
        Assert.True(login.TryGetProperty("traceId", out _));

        var refresh = await ProblemAsync(await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/refresh-token",
            new { refreshToken = "not-a-token" }), HttpStatusCode.Unauthorized);
        Assert.Equal("Auth.InvalidRefreshToken", refresh.GetProperty("code").GetString());

        var owner = await TestUsers.CreateAsync(factory);
        var circle = await (await owner.Client.PostAsJsonAsync("/api/v1/family-circles", new { name = "Codes" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var again = await ProblemAsync(await owner.Client.PostAsJsonAsync("/api/v1/family-circles/join",
            new { code = circle.GetProperty("joinCode").GetString() }), HttpStatusCode.Conflict);
        Assert.Equal("FamilyCircle.AlreadyMember", again.GetProperty("code").GetString());

        var missing = await ProblemAsync(await owner.Client.GetAsync($"/api/v1/family-circles/{Guid.NewGuid()}"), HttpStatusCode.NotFound);
        Assert.Equal("FamilyCircle.NotFound", missing.GetProperty("code").GetString());

        var (company, _) = await TestUsers.CreateCompanyAsync(factory);
        var groupId = await CompanyGroupTests.CreateTripGroupAsync(company);
        var notOnRoster = await ProblemAsync(await company.Client.PutAsJsonAsync(
            $"/api/v1/company/groups/{groupId}/supervisor", new { email = owner.Email }), HttpStatusCode.NotFound);
        Assert.Equal("Company.SupervisorNotFound", notOnRoster.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Validation_error_keys_are_camelCase_json_paths()
    {
        var register = await ProblemAsync(await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register",
            new { fullName = "", email = "bad", password = "short" }), HttpStatusCode.UnprocessableEntity);
        var keys = register.GetProperty("errors").EnumerateObject().Select(p => p.Name).ToList();
        Assert.Equal(["email", "fullName", "password"], keys.Order());
        Assert.Equal("Validation.Failed", register.GetProperty("code").GetString());

        var user = await TestUsers.CreateAsync(factory);
        var journey = await ProblemAsync(await user.Client.PostAsJsonAsync("/api/v1/journeys", new
        {
            pilgrimageType = 1,
            isFirstTime = true,
            travelParty = 1,
            arrival = new { mode = 1, entryPoint = "" },
            stays = new[] { new { cityName = "Makkah", accommodationType = 1, placeName = "", arrivalAtUtc = "2026-11-01T10:00:00Z" } }
        }), HttpStatusCode.UnprocessableEntity);
        var nested = journey.GetProperty("errors").EnumerateObject().Select(p => p.Name).ToList();
        Assert.Contains("arrival.entryPoint", nested);
        Assert.Contains("stays[0].placeName", nested);

        var route = await ProblemAsync(await user.Client.GetAsync("/api/v1/map/route?fromLat=21.4&fromLng=39.8"),
            HttpStatusCode.UnprocessableEntity);
        Assert.True(route.GetProperty("errors").TryGetProperty("destination", out _));
    }

    [Fact]
    public async Task Framework_authentication_and_authorization_errors_carry_codes()
    {
        var client = factory.CreateClient();
        var unauthorized = await ProblemAsync(await client.GetAsync("/api/v1/home"), HttpStatusCode.Unauthorized);
        Assert.Equal("Auth.Unauthorized", unauthorized.GetProperty("code").GetString());

        var user = await TestUsers.CreateAsync(factory);
        var forbidden = await ProblemAsync(await user.Client.GetAsync("/api/v1/admin/overview"), HttpStatusCode.Forbidden);
        Assert.Equal("Auth.Forbidden", forbidden.GetProperty("code").GetString());
    }
}
