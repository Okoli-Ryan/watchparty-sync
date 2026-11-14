using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WatchParty.Api.IntegrationTests.Infrastructure;
using WatchParty.Application.Identity.Login;
using WatchParty.Application.Identity.Queries;
using WatchParty.Domain.Identity;

namespace WatchParty.Api.IntegrationTests;

public sealed class IdentityApiTests(WatchPartyApiFactory factory) : IClassFixture<WatchPartyApiFactory>
{
    [Fact]
    public async Task HealthReady_IsPublicAndReportsTheDatabaseUp()
    {
        using var anonymous = factory.CreateClient();

        var response = await anonymous.GetAsync("/health/ready");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AnyApiCall_WithoutAToken_IsUnauthorized()
    {
        using var anonymous = factory.CreateClient();

        var response = await anonymous.GetAsync("/api/v1/rooms");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithCorrectPassword_ReturnsATokenThatWorks()
    {
        var email = $"login.{Guid.NewGuid():N}@example.com";
        await factory.CreateAccountAsync(email, "Login Tester", Role.User);
        using var anonymous = factory.CreateClient();

        var loginResponse = await anonymous.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = email.ToUpperInvariant(), password = WatchPartyApiFactory.AdminPassword });

        loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResult>();
        login!.User.Email.ShouldBe(email);

        anonymous.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        var profile = await anonymous.GetFromJsonAsync<UserProfileDto>("/api/v1/me");
        profile!.DisplayName.ShouldBe("Login Tester");
        profile.Role.ShouldBe("user");
    }

    [Fact]
    public async Task Login_WithWrongPassword_IsUnauthorizedProblemDetails()
    {
        var email = $"wrong.{Guid.NewGuid():N}@example.com";
        await factory.CreateAccountAsync(email, "Wrong Password", Role.User);
        using var anonymous = factory.CreateClient();

        var response = await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "not the password" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task CreateUser_ByOrdinaryUser_IsForbidden()
    {
        using var user = await factory.SignInAsNewUserAsync("Ordinary");

        var response = await user.Http.PostAsJsonAsync("/api/v1/users", NewAccount("someone@example.com"));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateUser_ByAdmin_CreatesTheAccountOnce()
    {
        using var admin = await factory.SignInAsNewUserAsync("Admin", Role.Admin);
        var email = $"new.{Guid.NewGuid():N}@example.com";

        var first = await admin.Http.PostAsJsonAsync("/api/v1/users", NewAccount(email));
        var duplicate = await admin.Http.PostAsJsonAsync("/api/v1/users", NewAccount(email));

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateUser_WithShortPassword_ReturnsFieldErrors()
    {
        using var admin = await factory.SignInAsNewUserAsync("Admin", Role.Admin);

        var response = await admin.Http.PostAsJsonAsync(
            "/api/v1/users",
            new { email = "short@example.com", password = "short", displayName = "Short", role = "user" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("\"Password\"");
    }

    private static object NewAccount(string email) => new
    {
        email,
        password = "a long enough password",
        displayName = "New Person",
        role = "user",
    };
}
