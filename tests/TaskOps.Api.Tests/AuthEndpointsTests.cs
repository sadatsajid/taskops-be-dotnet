using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using TaskOps.Api.Features.Auth;
using TaskOps.Api.Tests.Infrastructure;

namespace TaskOps.Api.Tests;

public sealed class AuthEndpointsTests(TaskOpsApiFactory factory) : IntegrationTestBase(factory), IClassFixture<TaskOpsApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Register_CreatesUserAndReturnsTokens()
    {
        var email = $"register-{Guid.NewGuid():N}@example.com";

        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            email,
            "Register Test",
            "Password123!"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<AuthResponse>>();
        envelope.Should().NotBeNull();
        envelope!.Success.Should().BeTrue();
        envelope.Data.AccessToken.Should().NotBeNullOrWhiteSpace();
        envelope.Data.RefreshToken.Should().NotBeNullOrWhiteSpace();
        envelope.Data.CurrentUser.Email.Should().Be(email);
    }

    [Fact]
    public async Task LoginAndMe_ReturnAuthenticatedUser()
    {
        var email = $"login-{Guid.NewGuid():N}@example.com";
        await _client.RegisterAsync(email, "Auth Test");

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginEnvelope = await loginResponse.Content.ReadFromJsonAsync<ApiResponseEnvelope<AuthResponse>>();

        _client.Authorize(loginEnvelope!.Data);

        var meResponse = await _client.GetFromJsonAsync<ApiResponseEnvelope<CurrentUserResponse>>("/api/auth/me");

        meResponse.Should().NotBeNull();
        meResponse!.Data.Email.Should().Be(email);
    }

    [Fact]
    public async Task Refresh_RotatesRefreshToken()
    {
        var email = $"refresh-{Guid.NewGuid():N}@example.com";
        var registered = await _client.RegisterAsync(email, "Auth Test");

        var refreshResponse = await _client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest(registered.RefreshToken));

        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshed = await refreshResponse.Content.ReadFromJsonAsync<ApiResponseEnvelope<AuthResponse>>();
        refreshed!.Data.RefreshToken.Should().NotBe(registered.RefreshToken);

        var oldRefreshResponse = await _client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest(registered.RefreshToken));
        oldRefreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_AllowsOnlyOneConcurrentRotation()
    {
        var email = $"refresh-concurrent-{Guid.NewGuid():N}@example.com";
        var registered = await _client.RegisterAsync(email, "Auth Test");

        var refreshRequests = Enumerable.Range(0, 2)
            .Select(_ => _client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest(registered.RefreshToken)))
            .ToArray();

        var responses = await Task.WhenAll(refreshRequests);

        responses.Count(response => response.StatusCode == HttpStatusCode.OK).Should().Be(1);
        responses.Count(response => response.StatusCode == HttpStatusCode.Unauthorized).Should().Be(1);
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        var email = $"logout-{Guid.NewGuid():N}@example.com";
        var registered = await _client.RegisterAsync(email, "Auth Test");

        _client.Authorize(registered);

        var logoutResponse = await _client.PostAsJsonAsync("/api/auth/logout", new LogoutRequest(registered.RefreshToken));
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var refreshResponse = await _client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest(registered.RefreshToken));
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflictProblemDetails()
    {
        var email = $"duplicate-{Guid.NewGuid():N}@example.com";
        await _client.RegisterAsync(email, "Auth Test");

        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            email.ToUpperInvariant(),
            "Duplicate Test",
            "Password123!"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"title\":\"Duplicate email.\"");
        json.Should().Contain("\"detail\":\"A user with this email already exists.\"");
    }

    [Fact]
    public async Task Login_WithWrongShortPassword_ReturnsUnauthorized()
    {
        var email = $"short-wrong-password-{Guid.NewGuid():N}@example.com";
        await _client.RegisterAsync(email, "Auth Test");

        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "wrong"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Register_WithOversizedFields_ReturnsValidationProblem()
    {
        var oversizedEmail = $"{new string('a', 321)}@example.com";
        var oversizedDisplayName = new string('b', 121);

        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            oversizedEmail,
            oversizedDisplayName,
            "Password123!"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("email");
        json.Should().Contain("displayName");
    }

}
