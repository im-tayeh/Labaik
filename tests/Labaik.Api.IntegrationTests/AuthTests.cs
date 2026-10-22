using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Labaik.Api.IntegrationTests.Infrastructure;

namespace Labaik.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AuthTests(LabaikApiFactory factory)
{
    [Fact]
    public async Task Register_verify_and_me_returns_the_user()
    {
        var user = await TestUsers.CreateAsync(factory, name: "Amina");

        var me = await user.Client.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");

        Assert.Equal(user.Id.ToString(), me.GetProperty("id").GetString());
        Assert.Equal("Amina", me.GetProperty("name").GetString());
        Assert.Equal(0, me.GetProperty("roles").GetArrayLength()); // pilgrim
    }

    [Fact]
    public async Task Login_before_verifying_email_is_forbidden()
    {
        var client = factory.CreateClient();
        var email = $"unverified-{Guid.NewGuid():N}@labaik.test";
        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new { fullName = "Unverified Pilgrim", email, password = TestUsers.Password });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = TestUsers.Password });

        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_is_unauthorized()
    {
        var user = await TestUsers.CreateAsync(factory);

        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password = "Wrong-pass1" });

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Invalid_input_returns_422_with_errors()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register",
            new { fullName = "", email = "not-an-email", password = "x" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").EnumerateObject().Any());
        Assert.True(problem.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task Forgot_password_never_reveals_whether_the_account_exists()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/forgot-password",
            new { email = $"nobody-{Guid.NewGuid():N}@labaik.test" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Resend_code_for_a_confirmed_account_sends_a_password_reset_code()
    {
        var user = await TestUsers.CreateAsync(factory);
        var resend = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/resend-code", new { email = user.Email });
        Assert.Equal(HttpStatusCode.OK, resend.StatusCode);

        var reset = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/reset-password", new
        {
            email = user.Email,
            code = factory.Emails.LastCodeFor(user.Email),
            newPassword = "Changed-Pass2!"
        });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email = user.Email, password = "Changed-Pass2!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task A_weak_new_password_does_not_use_up_the_reset_code()
    {
        var user = await TestUsers.CreateAsync(factory);
        var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await anonymous.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = $" {user.Email} " })).StatusCode);
        var code = factory.Emails.LastCodeFor(user.Email);

        var weak = await anonymous.PostAsJsonAsync("/api/v1/auth/reset-password", new { email = user.Email, code, newPassword = "Password1" });
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        Assert.Equal("Auth.WeakPassword", (await weak.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        var strong = await anonymous.PostAsJsonAsync("/api/v1/auth/reset-password", new { email = user.Email, code, newPassword = "Password1!" });
        Assert.Equal(HttpStatusCode.OK, strong.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password = "Password1!" })).StatusCode);
    }

    [Fact]
    public async Task Resetting_the_password_of_a_never_verified_signup_lets_it_sign_in()
    {
        var email = $"unverified-{Guid.NewGuid():N}@labaik.test";
        var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await anonymous.PostAsJsonAsync("/api/v1/auth/register", new { fullName = "Never Verified", email, password = TestUsers.Password })).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await anonymous.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email })).StatusCode);
        var reset = await anonymous.PostAsJsonAsync("/api/v1/auth/reset-password", new { email, code = factory.Emails.LastCodeFor(email), newPassword = "Changed-Pass3!" });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Changed-Pass3!" })).StatusCode);
    }

    [Fact]
    public async Task A_blocked_token_sent_along_does_not_block_public_auth_endpoints()
    {
        var admin = await TestUsers.GetAdminAsync(factory);
        var user = await TestUsers.CreateAsync(factory);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.Client.PostAsync($"/api/v1/admin/users/{user.Id}/disable", null)).StatusCode);

        // the app may still attach the old token: forgot-password must work and login must give the real reason
        Assert.Equal(HttpStatusCode.OK, (await user.Client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = user.Email })).StatusCode);
        var login = await user.Client.PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password = TestUsers.Password });
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
        Assert.Equal("Auth.AccountDisabled", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Refresh_token_is_single_use()
    {
        var user = await TestUsers.CreateAsync(factory);
        var client = factory.CreateClient();

        var first = await client.PostAsJsonAsync("/api/v1/auth/refresh-token", new { refreshToken = user.RefreshToken });
        var reuse = await client.PostAsJsonAsync("/api/v1/auth/refresh-token", new { refreshToken = user.RefreshToken });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, reuse.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_refresh_token_without_access_token()
    {
        var user = await TestUsers.CreateAsync(factory);
        var client = factory.CreateClient();

        var logout = await client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = user.RefreshToken });
        var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh-token", new { refreshToken = user.RefreshToken });

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Protected_endpoints_require_a_token()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/home");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
