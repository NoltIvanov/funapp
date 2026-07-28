using MauiApp1.Models;
using MauiApp1.Services;
using System.Text.Json;

namespace MauiApp1.Tests.Services;

public class UserSessionServiceTests
{
    [Fact]
    public async Task SaveSessionAsync_WritesSessionAndUpdatesCurrentValues()
    {
        var storageService = new FakeSecureStorageService();
        var service = new UserSessionService(storageService);
        var user = CreateUser();

        await service.SaveSessionAsync(user, "session-token");

        Assert.Same(user, service.CurrentUser);
        Assert.Equal("session-token", service.SessionToken);

        var storedJson = Assert.Single(storageService.Values).Value;
        using var document = JsonDocument.Parse(storedJson);
        Assert.Equal("Google", document.RootElement.GetProperty("User").GetProperty("Provider").GetString());
        Assert.Equal("session-token", document.RootElement.GetProperty("SessionToken").GetString());
    }

    [Fact]
    public async Task LoadSessionAsync_RestoresStoredSession()
    {
        var storageService = new FakeSecureStorageService();
        storageService.Values["user_session"] = JsonSerializer.Serialize(new
        {
            User = CreateUser(),
            SessionToken = "session-token",
        });
        var service = new UserSessionService(storageService);

        await service.LoadSessionAsync();

        Assert.Equal("Google", service.CurrentUser?.Provider);
        Assert.Equal("user-id", service.CurrentUser?.Id);
        Assert.Equal("User Name", service.CurrentUser?.Name);
        Assert.Equal("user@example.test", service.CurrentUser?.Email);
        Assert.Equal(new Uri("https://cdn.example.test/user.png"), service.CurrentUser?.Picture);
        Assert.Equal("session-token", service.SessionToken);
    }

    [Fact]
    public async Task LoadSessionAsync_ClearsSessionWhenStoredJsonIsInvalid()
    {
        var storageService = new FakeSecureStorageService();
        storageService.Values["user_session"] = "{";
        var service = new UserSessionService(storageService);

        await service.LoadSessionAsync();

        Assert.Null(service.CurrentUser);
        Assert.Null(service.SessionToken);
        Assert.Contains("user_session", storageService.RemovedKeys);
        Assert.Empty(storageService.Values);
    }

    [Fact]
    public async Task ClearSessionAsync_RemovesSessionAndCurrentValues()
    {
        var storageService = new FakeSecureStorageService();
        var service = new UserSessionService(storageService);
        await service.SaveSessionAsync(CreateUser(), "session-token");

        await service.ClearSessionAsync();

        Assert.Null(service.CurrentUser);
        Assert.Null(service.SessionToken);
        Assert.Contains("user_session", storageService.RemovedKeys);
        Assert.Empty(storageService.Values);
    }

    private static UserModel CreateUser()
    {
        return new UserModel
        {
            Provider = "Google",
            Id = "user-id",
            Name = "User Name",
            Email = "user@example.test",
            Picture = new Uri("https://cdn.example.test/user.png"),
        };
    }
}
