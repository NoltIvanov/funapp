using System.Net;
using System.Net.Sockets;
using System.Text;

#if WINDOWS
using Microsoft.Maui.ApplicationModel;
#endif

namespace MauiApp1.Services;

public class BrowserAuthenticationService : IBrowserAuthenticationService
{
    private static readonly TimeSpan WindowsAuthenticationTimeout = TimeSpan.FromMinutes(5);

    public async Task<BrowserAuthenticationResult> AuthenticateAsync(
        Func<string, Uri> authorizationUrlFactory,
        BrowserAuthenticationOptions options,
        CancellationToken cancellationToken = default)
    {
#if WINDOWS
        return await AuthenticateWithWindowsLoopbackAsync(
            authorizationUrlFactory,
            options.WindowsLoopbackHost,
            cancellationToken);
#else
        var result = await WebAuthenticator.AuthenticateAsync(new WebAuthenticatorOptions
        {
            Url = authorizationUrlFactory(options.CallbackUri),
            CallbackUrl = new Uri(options.CallbackUri),
            PrefersEphemeralWebBrowserSession = true
        });

        return new BrowserAuthenticationResult(
            new Dictionary<string, string>(result.Properties),
            options.CallbackUri);
#endif
    }

#if WINDOWS
    private static async Task<BrowserAuthenticationResult> AuthenticateWithWindowsLoopbackAsync(
        Func<string, Uri> authorizationUrlFactory,
        string host,
        CancellationToken cancellationToken)
    {
        var callbackUri = CreateLoopbackCallbackUri(host);
        var authorizationUrl = authorizationUrlFactory(callbackUri);

        using var listener = new HttpListener();
        listener.Prefixes.Add(callbackUri);
        listener.Start();

        if (!await Launcher.Default.OpenAsync(authorizationUrl))
            throw new InvalidOperationException("Unable to open the system browser for authentication.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(WindowsAuthenticationTimeout);

        var context = await listener.GetContextAsync().WaitAsync(timeout.Token);
        var requestUri = context.Request.Url
            ?? throw new InvalidOperationException("The authentication callback did not include a URL.");

        await WriteBrowserResponseAsync(context.Response, timeout.Token);

        return new BrowserAuthenticationResult(ParseQuery(requestUri.Query), callbackUri);
    }

    private static string CreateLoopbackCallbackUri(string host)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        return $"http://{host}:{port}/";
    }

    private static async Task WriteBrowserResponseAsync(
        HttpListenerResponse response,
        CancellationToken cancellationToken)
    {
        const string body = """
            <!doctype html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <title>Authentication complete</title>
            </head>
            <body>
                <p>Authentication complete. You can return to the app.</p>
            </body>
            </html>
            """;

        var buffer = Encoding.UTF8.GetBytes(body);
        response.StatusCode = 200;
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = buffer.Length;

        await response.OutputStream.WriteAsync(buffer, cancellationToken);
        response.Close();
    }

    private static IReadOnlyDictionary<string, string> ParseQuery(string query)
    {
        return query
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(parameter => parameter.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(
                parts => Uri.UnescapeDataString(parts[0].Replace("+", " ")),
                parts => Uri.UnescapeDataString(parts[1].Replace("+", " ")));
    }
#endif
}
