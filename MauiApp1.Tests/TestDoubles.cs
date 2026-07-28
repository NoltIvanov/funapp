using MauiApp1.Models;
using MauiApp1.Services;
using System.Net;
using System.Text;

namespace MauiApp1.Tests;

internal sealed class CapturingBrowserAuthenticationService : IBrowserAuthenticationService
{
    public BrowserAuthenticationOptions? LastOptions { get; private set; }
    public Uri? LastAuthorizationUrl { get; private set; }
    public Func<Uri, BrowserAuthenticationOptions, BrowserAuthenticationResult>? ResultFactory { get; set; }

    public Task<BrowserAuthenticationResult> AuthenticateAsync(
        Func<string, Uri> authorizationUrlFactory,
        BrowserAuthenticationOptions options,
        CancellationToken cancellationToken = default)
    {
        LastOptions = options;
        LastAuthorizationUrl = authorizationUrlFactory(options.CallbackUri);

        if (ResultFactory is not null)
            return Task.FromResult(ResultFactory(LastAuthorizationUrl, options));

        var query = TestQuery.Parse(LastAuthorizationUrl.Query);
        return Task.FromResult(new BrowserAuthenticationResult(
            new Dictionary<string, string>
            {
                ["state"] = query["state"],
                ["code"] = "authorization-code",
            },
            options.CallbackUri));
    }
}

internal sealed class FakeAppleAuthenticationService : IAppleAuthenticationService
{
    public bool IsAvailable { get; set; }
    public Func<Task<UserModel>> SignInAsyncHandler { get; set; } = () => Task.FromResult(new UserModel());

    public Task<UserModel> SignInAsync()
    {
        return SignInAsyncHandler();
    }
}

internal sealed class RecordingSessionService : IUserSessionService
{
    public List<(UserModel User, string? SessionToken)> SavedSessions { get; } = [];
    public int LoadSessionCalls { get; private set; }
    public int ClearSessionCalls { get; private set; }

    public UserModel? CurrentUser { get; private set; }
    public string? SessionToken { get; private set; }

    public Task LoadSessionAsync()
    {
        LoadSessionCalls++;
        return Task.CompletedTask;
    }

    public Task SaveSessionAsync(UserModel user, string? sessionToken)
    {
        SavedSessions.Add((user, sessionToken));
        CurrentUser = user;
        SessionToken = sessionToken;
        return Task.CompletedTask;
    }

    public Task ClearSessionAsync()
    {
        ClearSessionCalls++;
        CurrentUser = null;
        SessionToken = null;
        return Task.CompletedTask;
    }
}

internal sealed class FakeSecureStorageService : ISecureStorageService
{
    public Dictionary<string, string> Values { get; } = [];
    public List<string> RemovedKeys { get; } = [];
    public Exception? GetException { get; set; }

    public Task<string?> GetAsync(string key)
    {
        if (GetException is not null)
            throw GetException;

        Values.TryGetValue(key, out var value);
        return Task.FromResult<string?>(value);
    }

    public Task SetAsync(string key, string value)
    {
        Values[key] = value;
        return Task.CompletedTask;
    }

    public void Remove(string key)
    {
        Values.Remove(key);
        RemovedKeys.Add(key);
    }
}

internal sealed class DelegateHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _sendAsync;

    public List<Uri> RequestUris { get; } = [];

    public DelegateHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync)
    {
        _sendAsync = sendAsync;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestUris.Add(request.RequestUri!);
        return await _sendAsync(request, cancellationToken);
    }

    public static HttpResponseMessage Json(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }
}

internal sealed class StubAuthenticationService : IAuthenticationService
{
    public bool IsAppleSignInAvailable { get; set; }
    public Func<Task> SignInWithGoogleAsyncHandler { get; set; } = () => Task.CompletedTask;
    public Func<Task> SignInWithMicrosoftAsyncHandler { get; set; } = () => Task.CompletedTask;
    public Func<Task> SignInWithAppleAsyncHandler { get; set; } = () => Task.CompletedTask;

    public Task SignInWithGoogleAsync()
    {
        return SignInWithGoogleAsyncHandler();
    }

    public Task SignInWithMicrosoftAsync()
    {
        return SignInWithMicrosoftAsyncHandler();
    }

    public Task SignInWithAppleAsync()
    {
        return SignInWithAppleAsyncHandler();
    }
}

internal sealed class RecordingNavigationService : INavigationService
{
    public int GoToMainCalls { get; private set; }
    public int GoToLoginCalls { get; private set; }

    public Task GoToMainAsync()
    {
        GoToMainCalls++;
        return Task.CompletedTask;
    }

    public Task GoToLoginAsync()
    {
        GoToLoginCalls++;
        return Task.CompletedTask;
    }
}

internal sealed class RecordingDialogService : IDialogService
{
    public List<(string Title, string Message, string Cancel)> Alerts { get; } = [];

    public Task ShowAlertAsync(string title, string message, string cancel)
    {
        Alerts.Add((title, message, cancel));
        return Task.CompletedTask;
    }
}

internal sealed class EnvironmentVariableScope : IDisposable
{
    private readonly string _name;
    private readonly string? _oldValue;

    public EnvironmentVariableScope(string name, string? value)
    {
        _name = name;
        _oldValue = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(_name, _oldValue);
    }
}

internal static class TestQuery
{
    public static Dictionary<string, string> Parse(string query)
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
}
