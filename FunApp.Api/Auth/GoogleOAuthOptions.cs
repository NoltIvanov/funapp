namespace FunApp.Api.Auth;

public sealed class GoogleOAuthOptions
{
    public const string SectionName = "Authentication:Google";

    public string MobileClientId { get; init; } = string.Empty;
    public string DesktopClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public Uri TokenEndpoint { get; init; } = new("https://oauth2.googleapis.com/token");
    public Uri UserInfoEndpoint { get; init; } = new("https://openidconnect.googleapis.com/v1/userinfo");
}
