using MauiApp1.Models;

namespace MauiApp1.Services;

public interface IAppleAuthenticationService
{
    bool IsAvailable { get; }

    Task<UserModel> SignInAsync();
}
