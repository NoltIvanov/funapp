using System.Text.Json;
using System.Text.Json.Serialization;
using FunApp.Api.Contracts;
using Microsoft.Extensions.Options;

namespace FunApp.Api.Auth;

public sealed class GoogleOAuthClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly GoogleOAuthOptions _options;

    public GoogleOAuthClient(HttpClient httpClient, IOptions<GoogleOAuthOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<ExternalUserProfile> ExchangeCodeForProfileAsync(
        GoogleAuthorizationCodeExchangeRequest request,
        CancellationToken cancellationToken)
    {
        var client = ResolveClient(request.ClientId);
        var accessToken = await ExchangeCodeForAccessTokenAsync(request, client, cancellationToken);
        return await LoadProfileAsync(accessToken, cancellationToken);
    }

    private GoogleOAuthClientRegistration ResolveClient(string clientId)
    {
        if (string.Equals(clientId, _options.MobileClientId, StringComparison.Ordinal))
            return new GoogleOAuthClientRegistration(_options.MobileClientId, string.Empty);

        if (string.Equals(clientId, _options.DesktopClientId, StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(_options.ClientSecret))
                throw new OAuthConfigurationException(
                    "Authentication:Google:ClientSecret is required for the configured desktop OAuth client.");

            return new GoogleOAuthClientRegistration(_options.DesktopClientId, _options.ClientSecret);
        }

        throw new OAuthConfigurationException(
            "The supplied Google client_id is not configured in Authentication:Google:MobileClientId or Authentication:Google:DesktopClientId.");
    }

    private async Task<string> ExchangeCodeForAccessTokenAsync(
        GoogleAuthorizationCodeExchangeRequest request,
        GoogleOAuthClientRegistration client,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, string>
        {
            ["client_id"] = client.ClientId,
            ["redirect_uri"] = request.RedirectUri,
            ["grant_type"] = "authorization_code",
            ["code"] = request.AuthorizationCode,
            ["code_verifier"] = request.CodeVerifier,
        };

        if (!string.IsNullOrWhiteSpace(client.ClientSecret))
            parameters["client_secret"] = client.ClientSecret;

        using var response = await _httpClient.PostAsync(
            _options.TokenEndpoint,
            new FormUrlEncodedContent(parameters),
            cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new OAuthProviderException(CreateProviderErrorMessage(response, body));

        var token = JsonSerializer.Deserialize<GoogleTokenResponse>(body, JsonOptions)
            ?? throw new OAuthProviderException("Google returned an empty token response.");

        var accessToken = token.AccessToken;
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new OAuthProviderException("Google token response did not include access_token.");

        return accessToken;
    }

    private async Task<ExternalUserProfile> LoadProfileAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _options.UserInfoEndpoint);
        request.Headers.Authorization = new("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new OAuthProviderException(CreateProviderErrorMessage(response, body));

        var profile = JsonSerializer.Deserialize<GoogleUserInfoResponse>(body, JsonOptions)
            ?? throw new OAuthProviderException("Google returned an empty profile response.");

        if (string.IsNullOrWhiteSpace(profile.Subject))
            throw new OAuthProviderException("Google profile response did not include sub.");

        return new ExternalUserProfile(
            "Google",
            profile.Subject,
            profile.Name ?? string.Empty,
            profile.Email ?? string.Empty,
            profile.Picture);
    }

    private static string CreateProviderErrorMessage(HttpResponseMessage response, string body)
    {
        var providerError = TryReadProviderError(body);
        return string.IsNullOrWhiteSpace(providerError)
            ? $"Google returned HTTP {(int)response.StatusCode}."
            : $"Google returned HTTP {(int)response.StatusCode}: {providerError}";
    }

    private static string TryReadProviderError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return string.Empty;

        try
        {
            using var document = JsonDocument.Parse(body);
            var error = GetJsonString(document.RootElement, "error");
            var description = GetJsonString(document.RootElement, "error_description");

            return string.IsNullOrWhiteSpace(error) && string.IsNullOrWhiteSpace(description)
                ? string.Empty
                : $"{error}{(string.IsNullOrWhiteSpace(description) ? string.Empty : $" - {description}")}";
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static string GetJsonString(JsonElement json, string propertyName)
    {
        return json.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private sealed record GoogleOAuthClientRegistration(string ClientId, string ClientSecret);

    private sealed record GoogleTokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken);

    private sealed record GoogleUserInfoResponse(
        [property: JsonPropertyName("sub")] string? Subject,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("email")] string? Email,
        [property: JsonPropertyName("picture")] string? Picture);
}
