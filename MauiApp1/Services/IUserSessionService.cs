using MauiApp1.Models;

namespace MauiApp1.Services;

public interface IUserSessionService
{
    UserModel? CurrentUser { get; set; }
    string? SessionToken { get; set; }
}
