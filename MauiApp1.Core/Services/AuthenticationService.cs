using MauiApp1.Models;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MauiApp1.Services;

public class AuthenticationService : IAuthenticationService
{
    private const string GoogleClientId = "941430168156-87e8flf4huf7sr66foc61kotbb30lqtn.apps.googleusercontent.com";
    private const string GoogleWindowsClientId = "941430168156-c7mp4qb5go2179lcnleei77t7cfafosl.apps.googleusercontent.com";
    private const string GoogleWindowsClientSecretEnvironmentVariable = "GOOGLE_WINDOWS_CLIENT_SECRET";
    private const string FunAppApiBaseUrlEnvironmentVariable = "FUNAPP_API_BASE_URL";

    private static readonly AuthProvider GoogleProvider = new(
        AuthProviderKind.Google,
        "Google",
        new Uri("https://accounts.google.com/o/oauth2/v2/auth"),
        new Uri("https://oauth2.googleapis.com/token"),
        new Uri("https://openidconnect.googleapis.com/v1/userinfo"),
        GoogleClientId,
        CreateGoogleRedirectUri(GoogleClientId),
        GoogleWindowsClientId,
        GoogleWindowsClientSecretEnvironmentVariable,
        "127.0.0.1",
        "openid profile email");

    private static readonly AuthProvider MicrosoftProvider = new(
        AuthProviderKind.Microsoft,
        "Microsoft",
        new Uri("https://login.microsoftonline.com/common/oauth2/v2.0/authorize"),
        new Uri("https://login.microsoftonline.com/common/oauth2/v2.0/token"),
        new Uri("https://graph.microsoft.com/v1.0/me"),
        "84a5b2c7-cc24-4bc2-a272-33bf88b7a0f4",
        "msauth://com.wolfapp/d8KUJIGjAISv24pyqyv1QXT%2Fe64%3D",
        "84a5b2c7-cc24-4bc2-a272-33bf88b7a0f4",
        string.Empty,
        "localhost",
        "openid profile email User.Read");

    private readonly IBrowserAuthenticationService _browserAuthenticationService;
    private readonly IAppleAuthenticationService _appleAuthenticationService;
    private readonly IUserSessionService _userSessionService;
    private readonly HttpClient _httpClient;

    public bool IsAppleSignInAvailable => _appleAuthenticationService.IsAvailable;

    public AuthenticationService(
        IBrowserAuthenticationService browserAuthenticationService,
        IAppleAuthenticationService appleAuthenticationService,
        IUserSessionService userSessionService,
        HttpClient httpClient)
    {
        _browserAuthenticationService = browserAuthenticationService;
        _appleAuthenticationService = appleAuthenticationService;
        _userSessionService = userSessionService;
        _httpClient = httpClient;
    }

    public async Task SignInWithGoogleAsync()
    {
        await SignInWithOAuthAsync(GoogleProvider);
    }

    public async Task SignInWithMicrosoftAsync()
    {
        await SignInWithOAuthAsync(MicrosoftProvider);
    }

    public async Task SignInWithAppleAsync()
    {
        await _userSessionService.SaveSessionAsync(
            await _appleAuthenticationService.SignInAsync(),
            null);
    }

    private async Task SignInWithOAuthAsync(AuthProvider provider)
    {
        var request = CreateAuthRequest();

        var result = await _browserAuthenticationService.AuthenticateAsync(
            callbackUri => BuildAuthUrl(provider, request, callbackUri),
            new BrowserAuthenticationOptions(provider.RedirectUri, provider.WindowsLoopbackHost));

        var authorizationCode = ValidateAuthResult(result.Properties, request.State, provider.Name);
        if (provider.Kind == AuthProviderKind.Google && GetBackendBaseUri() is { } backendBaseUri)
        {
            var backendResult = await ExchangeGoogleCodeWithBackendAsync(
                provider,
                request,
                authorizationCode,
                result.CallbackUri,
                backendBaseUri);

            await _userSessionService.SaveSessionAsync(
                backendResult.User,
                backendResult.SessionToken);

            return;
        }

        var accessToken = await ExchangeCodeForAccessTokenAsync(
            provider,
            request,
            authorizationCode,
            result.CallbackUri);

        await _userSessionService.SaveSessionAsync(
            await LoadUserProfileAsync(provider, accessToken),
            null);
    }

    private static AuthRequest CreateAuthRequest()
    {
        var codeVerifier = CreateRandomToken();
        return new AuthRequest(
            CreateRandomToken(),
            CreateRandomToken(),
            codeVerifier,
            CreateCodeChallenge(codeVerifier));
    }

    private static Uri BuildAuthUrl(AuthProvider provider, AuthRequest request, string redirectUri)
    {
        var parameters = new Dictionary<string, string>
        {
            ["client_id"] = GetClientId(provider),
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = provider.Scopes,
            ["response_mode"] = "query",
            ["state"] = request.State,
            ["nonce"] = request.Nonce,
            ["code_challenge"] = request.CodeChallenge,
            ["code_challenge_method"] = "S256",
        };

        var queryString = string.Join("&", parameters.Select(parameter =>
            $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value)}"));

        return new Uri($"{provider.Authority}?{queryString}");
    }

    private static string ValidateAuthResult(
        IReadOnlyDictionary<string, string> result,
        string expectedState,
        string providerName)
    {
        if (result.TryGetValue("error", out var error))
        {
            var description = result.TryGetValue("error_description", out var value)
                ? $": {value}"
                : string.Empty;
            var message = $"{providerName} returned '{error}'{description}";

            throw new InvalidOperationException(
                message.EndsWith(".", StringComparison.Ordinal)
                    ? message
                    : $"{message}.");
        }

        if (!result.TryGetValue("state", out var returnedState) ||
            !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(returnedState),
                Encoding.UTF8.GetBytes(expectedState)))
        {
            throw new InvalidOperationException($"{providerName} returned an invalid authentication state.");
        }

        if (!result.TryGetValue("code", out var authorizationCode) ||
            string.IsNullOrWhiteSpace(authorizationCode))
        {
            throw new InvalidOperationException($"{providerName} did not return an authorization code.");
        }

        return authorizationCode;
    }

    private async Task<string> ExchangeCodeForAccessTokenAsync(
        AuthProvider provider,
        AuthRequest request,
        string authorizationCode,
        string redirectUri)
    {
        var parameters = new Dictionary<string, string>
        {
            ["client_id"] = GetClientId(provider),
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code",
            ["code"] = authorizationCode,
            ["code_verifier"] = request.CodeVerifier,
        };

        var clientSecret = GetClientSecret(provider);
        if (!string.IsNullOrWhiteSpace(clientSecret))
            parameters["client_secret"] = clientSecret;

        using var response = await _httpClient.PostAsync(provider.TokenEndpoint, new FormUrlEncodedContent(parameters));
        var tokenJson = await ReadJsonAsync(response, $"{provider.Name} token exchange");

        return GetRequiredJsonString(tokenJson, "access_token", $"{provider.Name} token response");
    }

    private async Task<BackendAuthResult> ExchangeGoogleCodeWithBackendAsync(
        AuthProvider provider,
        AuthRequest request,
        string authorizationCode,
        string redirectUri,
        Uri backendBaseUri)
    {
        var payload = new GoogleBackendExchangeRequest(
            authorizationCode,
            request.CodeVerifier,
            redirectUri,
            GetClientId(provider),
            GetPlatformName());

        var endpoint = new Uri(backendBaseUri, "auth/google/exchange");
        var requestBody = JsonSerializer.Serialize(payload);

        using var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(endpoint, content);
        var json = await ReadJsonAsync(response, "Google backend token exchange");

        var sessionToken = GetRequiredJsonString(json, "sessionToken", "Google backend response");
        if (!json.TryGetProperty("user", out var userJson) || userJson.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Google backend response did not include user.");

        return new BackendAuthResult(sessionToken, CreateBackendUser(userJson));
    }

    private static Uri? GetBackendBaseUri()
    {
        var configuredBaseUrl = Environment.GetEnvironmentVariable(FunAppApiBaseUrlEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(configuredBaseUrl))
            return null;

        if (!configuredBaseUrl.EndsWith("/", StringComparison.Ordinal))
            configuredBaseUrl += "/";

        if (!Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var backendBaseUri))
        {
            throw new InvalidOperationException(
                $"{FunAppApiBaseUrlEnvironmentVariable} must be an absolute URI.");
        }

        return backendBaseUri;
    }

    private static string GetClientId(AuthProvider provider)
    {
#if WINDOWS
        var clientId = provider.WindowsClientId;
#else
        var clientId = provider.ClientId;
#endif
        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException($"{provider.Name} sign-in requires an OAuth client ID for this platform.");

        return clientId;
    }

    private static string GetClientSecret(AuthProvider provider)
    {
#if WINDOWS
        if (string.IsNullOrWhiteSpace(provider.WindowsClientSecretEnvironmentVariable))
            return string.Empty;

        var clientSecret = Environment.GetEnvironmentVariable(provider.WindowsClientSecretEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException(
                $"{provider.Name} sign-in requires the {provider.WindowsClientSecretEnvironmentVariable} environment variable on Windows.");
        }

        return clientSecret;
#else
        return string.Empty;
#endif
    }

    private static string CreateGoogleRedirectUri(string clientId)
    {
        const string clientIdSuffix = ".apps.googleusercontent.com";

        if (!clientId.EndsWith(clientIdSuffix, StringComparison.Ordinal))
            return string.Empty;

        return $"com.googleusercontent.apps.{clientId[..^clientIdSuffix.Length]}:/oauth2callback";
    }

    private async Task<UserModel> LoadUserProfileAsync(AuthProvider provider, string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, provider.UserInfoEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request);
        var profileJson = await ReadJsonAsync(response, $"{provider.Name} profile request");

        return provider.Kind switch
        {
            AuthProviderKind.Google => CreateGoogleUser(profileJson),
            AuthProviderKind.Microsoft => CreateMicrosoftUser(profileJson),
            _ => throw new InvalidOperationException($"Unsupported auth provider: {provider.Name}."),
        };
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response, string operationDescription)
    {
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"{operationDescription} failed with HTTP {(int)response.StatusCode}{CreateResponseErrorDescription(responseBody)}.");
        }

        using var document = JsonDocument.Parse(responseBody);
        return document.RootElement.Clone();
    }

    private static string CreateResponseErrorDescription(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return string.Empty;

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var error = GetJsonString(document.RootElement, "error");
            var description = GetJsonString(document.RootElement, "error_description");

            if (string.IsNullOrWhiteSpace(error) && string.IsNullOrWhiteSpace(description))
            {
                error = GetJsonString(document.RootElement, "title");
                description = GetJsonString(document.RootElement, "detail");
            }

            return string.IsNullOrWhiteSpace(error) && string.IsNullOrWhiteSpace(description)
                ? string.Empty
                : $": {error}{(string.IsNullOrWhiteSpace(description) ? string.Empty : $" - {description}")}";
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static UserModel CreateGoogleUser(JsonElement profile)
    {
        return new UserModel
        {
            Provider = GoogleProvider.Name,
            Id = GetJsonString(profile, "sub"),
            Name = GetJsonString(profile, "name"),
            Email = GetJsonString(profile, "email"),
            Picture = CreateUriOrNull(GetJsonString(profile, "picture")),
        };
    }

    private static UserModel CreateBackendUser(JsonElement user)
    {
        return new UserModel
        {
            Provider = GetJsonString(user, "provider"),
            Id = GetJsonString(user, "id"),
            Name = GetJsonString(user, "name"),
            Email = GetJsonString(user, "email"),
            Picture = CreateUriOrNull(GetJsonString(user, "picture")),
        };
    }

    private static UserModel CreateMicrosoftUser(JsonElement profile)
    {
        var email = GetJsonString(profile, "mail");
        if (string.IsNullOrWhiteSpace(email))
            email = GetJsonString(profile, "userPrincipalName");

        return new UserModel
        {
            Provider = MicrosoftProvider.Name,
            Id = GetJsonString(profile, "id"),
            Name = GetJsonString(profile, "displayName"),
            Email = email,
        };
    }

    private static string GetRequiredJsonString(JsonElement json, string propertyName, string description)
    {
        var value = GetJsonString(json, propertyName);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{description} did not include '{propertyName}'.");

        return value;
    }

    private static string GetJsonString(JsonElement json, string propertyName)
    {
        return json.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static Uri? CreateUriOrNull(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null;
    }

    private static string CreateCodeChallenge(string codeVerifier)
    {
        return Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));
    }

    private static string CreateRandomToken()
    {
        return Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string GetPlatformName()
    {
#if WINDOWS
        return "windows";
#elif ANDROID
        return "android";
#elif IOS
        return "ios";
#elif MACCATALYST
        return "maccatalyst";
#else
        return "unknown";
#endif
    }

    private enum AuthProviderKind
    {
        Google,
        Microsoft,
    }

    private sealed record AuthProvider(
        AuthProviderKind Kind,
        string Name,
        Uri Authority,
        Uri TokenEndpoint,
        Uri UserInfoEndpoint,
        string ClientId,
        string RedirectUri,
        string WindowsClientId,
        string WindowsClientSecretEnvironmentVariable,
        string WindowsLoopbackHost,
        string Scopes);

    private sealed record AuthRequest(
        string State,
        string Nonce,
        string CodeVerifier,
        string CodeChallenge);

    private sealed record GoogleBackendExchangeRequest(
        string AuthorizationCode,
        string CodeVerifier,
        string RedirectUri,
        string ClientId,
        string Platform);

    private sealed record BackendAuthResult(
        string SessionToken,
        UserModel User);
}
