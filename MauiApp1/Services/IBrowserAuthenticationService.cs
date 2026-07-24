namespace MauiApp1.Services;

public interface IBrowserAuthenticationService
{
    Task<BrowserAuthenticationResult> AuthenticateAsync(
        Func<string, Uri> authorizationUrlFactory,
        BrowserAuthenticationOptions options,
        CancellationToken cancellationToken = default);
}

public sealed record BrowserAuthenticationOptions(
    string CallbackUri,
    string WindowsLoopbackHost);

public sealed record BrowserAuthenticationResult(
    IReadOnlyDictionary<string, string> Properties,
    string CallbackUri);
