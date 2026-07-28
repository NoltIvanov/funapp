using MauiApp1.Models;
using MauiApp1.Services;
using System.Net;
using System.Text.Json;

namespace MauiApp1.Tests.Services;

public class AuthenticationServiceTests
{
    private const string BackendBaseUrlEnvironmentVariable = "FUNAPP_API_BASE_URL";

    [Fact]
    public async Task SignInWithMicrosoftAsync_ExchangesCodeLoadsProfileAndSavesSession()
    {
        using var environment = new EnvironmentVariableScope(BackendBaseUrlEnvironmentVariable, null);
        var browserAuthenticationService = new CapturingBrowserAuthenticationService();
        var appleAuthenticationService = new FakeAppleAuthenticationService();
        var sessionService = new RecordingSessionService();

        var handler = new DelegateHttpMessageHandler(async (request, cancellationToken) =>
        {
            if (request.RequestUri == new Uri("https://login.microsoftonline.com/common/oauth2/v2.0/token"))
            {
                var body = TestQuery.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
                Assert.Equal("authorization_code", body["grant_type"]);
                Assert.Equal("authorization-code", body["code"]);
                Assert.Equal("msauth://com.wolfapp/d8KUJIGjAISv24pyqyv1QXT%2Fe64%3D", body["redirect_uri"]);
                Assert.False(string.IsNullOrWhiteSpace(body["code_verifier"]));

                return DelegateHttpMessageHandler.Json("""{"access_token":"graph-access-token"}""");
            }

            if (request.RequestUri == new Uri("https://graph.microsoft.com/v1.0/me"))
            {
                Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
                Assert.Equal("graph-access-token", request.Headers.Authorization?.Parameter);

                return DelegateHttpMessageHandler.Json(
                    """{"id":"microsoft-user","displayName":"Ada Lovelace","userPrincipalName":"ada@example.test"}""");
            }

            throw new InvalidOperationException($"Unexpected HTTP request: {request.RequestUri}");
        });

        using var httpClient = new HttpClient(handler);
        var service = new AuthenticationService(
            browserAuthenticationService,
            appleAuthenticationService,
            sessionService,
            httpClient);

        await service.SignInWithMicrosoftAsync();

        Assert.Equal("msauth://com.wolfapp/d8KUJIGjAISv24pyqyv1QXT%2Fe64%3D", browserAuthenticationService.LastOptions?.CallbackUri);
        Assert.Equal("localhost", browserAuthenticationService.LastOptions?.WindowsLoopbackHost);

        var authorizationQuery = TestQuery.Parse(browserAuthenticationService.LastAuthorizationUrl!.Query);
        Assert.Equal("84a5b2c7-cc24-4bc2-a272-33bf88b7a0f4", authorizationQuery["client_id"]);
        Assert.Equal("code", authorizationQuery["response_type"]);
        Assert.Contains("User.Read", authorizationQuery["scope"]);
        Assert.Equal("S256", authorizationQuery["code_challenge_method"]);
        Assert.False(string.IsNullOrWhiteSpace(authorizationQuery["state"]));
        Assert.False(string.IsNullOrWhiteSpace(authorizationQuery["nonce"]));
        Assert.DoesNotContain("=", authorizationQuery["code_challenge"]);

        Assert.Equal("Microsoft", sessionService.CurrentUser?.Provider);
        Assert.Equal("microsoft-user", sessionService.CurrentUser?.Id);
        Assert.Equal("Ada Lovelace", sessionService.CurrentUser?.Name);
        Assert.Equal("ada@example.test", sessionService.CurrentUser?.Email);
        Assert.Null(sessionService.SessionToken);
        Assert.Equal(2, handler.RequestUris.Count);
    }

    [Fact]
    public async Task SignInWithGoogleAsync_WithoutBackend_ExchangesTokenLoadsProfileAndSavesSession()
    {
        using var environment = new EnvironmentVariableScope(BackendBaseUrlEnvironmentVariable, null);
        var browserAuthenticationService = new CapturingBrowserAuthenticationService();
        var sessionService = new RecordingSessionService();

        var handler = new DelegateHttpMessageHandler(async (request, cancellationToken) =>
        {
            if (request.RequestUri == new Uri("https://oauth2.googleapis.com/token"))
            {
                var body = TestQuery.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
                Assert.Equal("authorization_code", body["grant_type"]);
                Assert.Equal("authorization-code", body["code"]);
                Assert.StartsWith("com.googleusercontent.apps.", body["redirect_uri"]);
                Assert.False(string.IsNullOrWhiteSpace(body["code_verifier"]));

                return DelegateHttpMessageHandler.Json("""{"access_token":"google-access-token"}""");
            }

            if (request.RequestUri == new Uri("https://openidconnect.googleapis.com/v1/userinfo"))
            {
                Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
                Assert.Equal("google-access-token", request.Headers.Authorization?.Parameter);

                return DelegateHttpMessageHandler.Json(
                    """{"sub":"google-user","name":"Grace Hopper","email":"grace@example.test","picture":"https://cdn.example.test/grace.png"}""");
            }

            throw new InvalidOperationException($"Unexpected HTTP request: {request.RequestUri}");
        });

        using var httpClient = new HttpClient(handler);
        var service = new AuthenticationService(
            browserAuthenticationService,
            new FakeAppleAuthenticationService(),
            sessionService,
            httpClient);

        await service.SignInWithGoogleAsync();

        Assert.StartsWith("com.googleusercontent.apps.", browserAuthenticationService.LastOptions?.CallbackUri);
        Assert.Equal("127.0.0.1", browserAuthenticationService.LastOptions?.WindowsLoopbackHost);

        var authorizationQuery = TestQuery.Parse(browserAuthenticationService.LastAuthorizationUrl!.Query);
        Assert.Equal("openid profile email", authorizationQuery["scope"]);
        Assert.Equal("S256", authorizationQuery["code_challenge_method"]);

        Assert.Equal("Google", sessionService.CurrentUser?.Provider);
        Assert.Equal("google-user", sessionService.CurrentUser?.Id);
        Assert.Equal("Grace Hopper", sessionService.CurrentUser?.Name);
        Assert.Equal("grace@example.test", sessionService.CurrentUser?.Email);
        Assert.Equal(new Uri("https://cdn.example.test/grace.png"), sessionService.CurrentUser?.Picture);
        Assert.Null(sessionService.SessionToken);
        Assert.Equal(2, handler.RequestUris.Count);
    }

    [Fact]
    public async Task SignInWithGoogleAsync_WithBackend_ExchangesCodeWithBackendAndSavesSessionToken()
    {
        using var environment = new EnvironmentVariableScope(
            BackendBaseUrlEnvironmentVariable,
            "https://api.example.test/base");
        var browserAuthenticationService = new CapturingBrowserAuthenticationService();
        var sessionService = new RecordingSessionService();

        JsonElement payload = default;
        var handler = new DelegateHttpMessageHandler(async (request, cancellationToken) =>
        {
            Assert.Equal(new Uri("https://api.example.test/base/auth/google/exchange"), request.RequestUri);
            Assert.Equal(HttpMethod.Post, request.Method);

            using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            payload = document.RootElement.Clone();

            return DelegateHttpMessageHandler.Json(
                """
                {
                    "sessionToken": "backend-session-token",
                    "user": {
                        "provider": "Google",
                        "id": "backend-user",
                        "name": "Katherine Johnson",
                        "email": "katherine@example.test",
                        "picture": "https://cdn.example.test/katherine.png"
                    }
                }
                """);
        });

        using var httpClient = new HttpClient(handler);
        var service = new AuthenticationService(
            browserAuthenticationService,
            new FakeAppleAuthenticationService(),
            sessionService,
            httpClient);

        await service.SignInWithGoogleAsync();

        Assert.Equal("authorization-code", payload.GetProperty("AuthorizationCode").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("CodeVerifier").GetString()));
        Assert.StartsWith("com.googleusercontent.apps.", payload.GetProperty("RedirectUri").GetString());
        Assert.Equal("unknown", payload.GetProperty("Platform").GetString());

        Assert.Equal("Google", sessionService.CurrentUser?.Provider);
        Assert.Equal("backend-user", sessionService.CurrentUser?.Id);
        Assert.Equal("backend-session-token", sessionService.SessionToken);
        Assert.Single(handler.RequestUris);
    }

    [Fact]
    public async Task SignInWithMicrosoftAsync_ThrowsWhenProviderReturnsError()
    {
        using var environment = new EnvironmentVariableScope(BackendBaseUrlEnvironmentVariable, null);
        var browserAuthenticationService = new CapturingBrowserAuthenticationService
        {
            ResultFactory = (_, options) => new BrowserAuthenticationResult(
                new Dictionary<string, string>
                {
                    ["error"] = "access_denied",
                    ["error_description"] = "The user denied access.",
                },
                options.CallbackUri),
        };
        var sessionService = new RecordingSessionService();

        using var httpClient = new HttpClient(new DelegateHttpMessageHandler((request, _) =>
            throw new InvalidOperationException($"Unexpected HTTP request: {request.RequestUri}")));
        var service = new AuthenticationService(
            browserAuthenticationService,
            new FakeAppleAuthenticationService(),
            sessionService,
            httpClient);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            service.SignInWithMicrosoftAsync);

        Assert.Equal("Microsoft returned 'access_denied': The user denied access.", exception.Message);
        Assert.Empty(sessionService.SavedSessions);
    }

    [Fact]
    public async Task SignInWithGoogleAsync_ThrowsWhenStateDoesNotMatch()
    {
        using var environment = new EnvironmentVariableScope(BackendBaseUrlEnvironmentVariable, null);
        var browserAuthenticationService = new CapturingBrowserAuthenticationService
        {
            ResultFactory = (_, options) => new BrowserAuthenticationResult(
                new Dictionary<string, string>
                {
                    ["state"] = "different-state",
                    ["code"] = "authorization-code",
                },
                options.CallbackUri),
        };
        var sessionService = new RecordingSessionService();

        using var httpClient = new HttpClient(new DelegateHttpMessageHandler((request, _) =>
            throw new InvalidOperationException($"Unexpected HTTP request: {request.RequestUri}")));
        var service = new AuthenticationService(
            browserAuthenticationService,
            new FakeAppleAuthenticationService(),
            sessionService,
            httpClient);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            service.SignInWithGoogleAsync);

        Assert.Equal("Google returned an invalid authentication state.", exception.Message);
        Assert.Empty(sessionService.SavedSessions);
    }

    [Fact]
    public async Task SignInWithAppleAsync_DelegatesToAppleServiceAndSavesSession()
    {
        var appleUser = new UserModel
        {
            Provider = "Apple",
            Id = "apple-user",
            Name = "Margaret Hamilton",
            Email = "margaret@example.test",
        };
        var appleAuthenticationService = new FakeAppleAuthenticationService
        {
            IsAvailable = true,
            SignInAsyncHandler = () => Task.FromResult(appleUser),
        };
        var sessionService = new RecordingSessionService();

        using var httpClient = new HttpClient(new DelegateHttpMessageHandler((request, _) =>
            throw new InvalidOperationException($"Unexpected HTTP request: {request.RequestUri}")));
        var service = new AuthenticationService(
            new CapturingBrowserAuthenticationService(),
            appleAuthenticationService,
            sessionService,
            httpClient);

        await service.SignInWithAppleAsync();

        Assert.True(service.IsAppleSignInAvailable);
        Assert.Same(appleUser, sessionService.CurrentUser);
        Assert.Null(sessionService.SessionToken);
    }
}
