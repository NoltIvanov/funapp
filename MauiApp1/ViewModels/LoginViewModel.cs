using MauiApp1.Models;
using MauiApp1.Services;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Input;

namespace MauiApp1.ViewModels
{
    public class LoginViewModel : BaseViewModel
    {
        private const string GoogleClientId = "941430168156-87e8flf4huf7sr66foc61kotbb30lqtn.apps.googleusercontent.com";
        private const string GoogleWindowsClientId = "941430168156-c7mp4qb5go2179lcnleei77t7cfafosl.apps.googleusercontent.com";
        private const string GoogleWindowsClientSecretEnvironmentVariable = "GOOGLE_WINDOWS_CLIENT_SECRET";

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

        private static readonly HttpClient HttpClient = new();

        private readonly INavigationService _navigationService;
        private readonly IUserSessionService _userSessionService;
        private readonly IBrowserAuthenticationService _browserAuthenticationService;
        private readonly IAppleAuthenticationService _appleAuthenticationService;

        public ICommand LoginWithGoogleCommand { get; }
        public ICommand LoginWithMicrosoftCommand { get; }
        public ICommand LoginWithAppleCommand { get; }
        public bool IsAppleSignInAvailable => _appleAuthenticationService.IsAvailable;

        public LoginViewModel(
            INavigationService navigationService,
            IUserSessionService userSessionService,
            IBrowserAuthenticationService browserAuthenticationService,
            IAppleAuthenticationService appleAuthenticationService)
        {
            _navigationService = navigationService;
            _userSessionService = userSessionService;
            _browserAuthenticationService = browserAuthenticationService;
            _appleAuthenticationService = appleAuthenticationService;

            LoginWithGoogleCommand = new Command(async () => await LoginAsync(GoogleProvider));
            LoginWithMicrosoftCommand = new Command(async () => await LoginAsync(MicrosoftProvider));
            LoginWithAppleCommand = new Command(async () => await LoginWithAppleAsync());
        }

        private async Task LoginAsync(AuthProvider provider)
        {
            if (IsBusy) return;

            try
            {
                IsBusy = true;
                var request = CreateAuthRequest();

                var result = await _browserAuthenticationService.AuthenticateAsync(
                    callbackUri => BuildAuthUrl(provider, request, callbackUri),
                    new BrowserAuthenticationOptions(provider.RedirectUri, provider.WindowsLoopbackHost));

                var authorizationCode = ValidateAuthResult(result.Properties, request.State, provider.Name);
                var accessToken = await ExchangeCodeForAccessTokenAsync(
                    provider,
                    request,
                    authorizationCode,
                    result.CallbackUri);
                _userSessionService.CurrentUser = await LoadUserProfileAsync(provider, accessToken);

                await _navigationService.GoToMainAsync();
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Error", $"{provider.Name} sign-in failed: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoginWithAppleAsync()
        {
            if (IsBusy) return;

            try
            {
                IsBusy = true;
                _userSessionService.CurrentUser = await _appleAuthenticationService.SignInAsync();

                await _navigationService.GoToMainAsync();
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Error", $"Apple sign-in failed: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false;
            }
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

                throw new InvalidOperationException($"{providerName} returned '{error}'{description}.");
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

        private static async Task<string> ExchangeCodeForAccessTokenAsync(
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

            using var response = await HttpClient.PostAsync(provider.TokenEndpoint, new FormUrlEncodedContent(parameters));
            var tokenJson = await ReadJsonAsync(response, $"{provider.Name} token exchange");

            return GetRequiredJsonString(tokenJson, "access_token", $"{provider.Name} token response");
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

        private static async Task<UserModel> LoadUserProfileAsync(AuthProvider provider, string accessToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, provider.UserInfoEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await HttpClient.SendAsync(request);
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
    }
}
