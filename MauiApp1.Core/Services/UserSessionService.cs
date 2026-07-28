using MauiApp1.Models;
using System.Text.Json;

namespace MauiApp1.Services;

public class UserSessionService : IUserSessionService
{
    private const string SessionKey = "user_session";

    private readonly ISecureStorageService _secureStorageService;

    public UserModel? CurrentUser { get; private set; }
    public string? SessionToken { get; private set; }

    public UserSessionService(ISecureStorageService secureStorageService)
    {
        _secureStorageService = secureStorageService;
    }

    public async Task LoadSessionAsync()
    {
        try
        {
            var sessionJson = await _secureStorageService.GetAsync(SessionKey);
            if (string.IsNullOrWhiteSpace(sessionJson))
            {
                CurrentUser = null;
                SessionToken = null;
                return;
            }

            var session = JsonSerializer.Deserialize<PersistedUserSession>(sessionJson);
            CurrentUser = session?.User;
            SessionToken = session?.SessionToken;
        }
        catch
        {
            await ClearSessionAsync();
        }
    }

    public async Task SaveSessionAsync(UserModel user, string? sessionToken)
    {
        var session = new PersistedUserSession(user, sessionToken);
        await _secureStorageService.SetAsync(SessionKey, JsonSerializer.Serialize(session));

        CurrentUser = user;
        SessionToken = sessionToken;
    }

    public Task ClearSessionAsync()
    {
        CurrentUser = null;
        SessionToken = null;

        _secureStorageService.Remove(SessionKey);

        return Task.CompletedTask;
    }

    private sealed record PersistedUserSession(UserModel User, string? SessionToken);
}
