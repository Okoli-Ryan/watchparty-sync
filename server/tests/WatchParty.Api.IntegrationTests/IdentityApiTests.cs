using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WatchParty.Api.IntegrationTests.Infrastructure;
using WatchParty.Application.Identity.Login;
using WatchParty.Application.Identity.Queries;

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
        await factory.CreateAccountAsync(email, "Login Tester");
        using var anonymous = factory.CreateClient();

        var loginResponse = await anonymous.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = email.ToUpperInvariant(), password = WatchPartyApiFactory.Password });

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
        await factory.CreateAccountAsync(email, "Wrong Password");
        using var anonymous = factory.CreateClient();

        var response = await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "not the password" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task SignUp_CreatesAnOrdinaryUserAndReturnsAWorkingToken()
    {
        var email = $"New.{Guid.NewGuid():N}@Example.com";
        using var anonymous = factory.CreateClient();

        var response = await anonymous.PostAsJsonAsync("/api/v1/auth/register", NewAccount(email));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var signUp = await response.Content.ReadFromJsonAsync<LoginResult>();
        signUp!.User.Role.ShouldBe("user");
        signUp.User.DisplayName.ShouldBe("New Person");
        signUp.User.Email.ShouldBe(email.ToLowerInvariant());

        anonymous.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", signUp.AccessToken);
        (await anonymous.GetAsync("/api/v1/me")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // And the same credentials sign in afterwards.
        using var second = factory.CreateClient();
        var login = await second.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "a long enough password" });
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SignUp_WithATakenEmail_IsConflict()
    {
        var email = $"taken.{Guid.NewGuid():N}@example.com";
        using var anonymous = factory.CreateClient();

        var first = await anonymous.PostAsJsonAsync("/api/v1/auth/register", NewAccount(email));
        var duplicate = await anonymous.PostAsJsonAsync("/api/v1/auth/register", NewAccount(email.ToUpperInvariant()));

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task SignUp_WithBadInput_ReturnsFieldErrors()
    {
        using var anonymous = factory.CreateClient();

        var response = await anonymous.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email = "short@example.com", displayName = "", password = "short" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("\"Password\"");
        body.ShouldContain("\"DisplayName\"");
    }

    [Fact]
    public async Task SignUp_CannotChooseARole()
    {
        var email = $"sneaky.{Guid.NewGuid():N}@example.com";
        using var anonymous = factory.CreateClient();

        var response = await anonymous.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email, displayName = "Sneaky", password = WatchPartyApiFactory.Password, role = "admin" });

        var signUp = await response.Content.ReadFromJsonAsync<LoginResult>();
        signUp!.User.Role.ShouldBe("user");
    }

    private static object NewAccount(string email) => new
    {
        email,
        displayName = "New Person",
        password = "a long enough password",
    };
}
