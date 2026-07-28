using MauiApp1.Models;

namespace MauiApp1.Services;

public interface IUserSessionService
{
    UserModel? CurrentUser { get; }
    string? SessionToken { get; }

    Task LoadSessionAsync();
    Task SaveSessionAsync(UserModel user, string? sessionToken);
    Task ClearSessionAsync();
}
