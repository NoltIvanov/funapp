using MauiApp1.Models;

namespace MauiApp1.Services;

public class UserSessionService : IUserSessionService
{
    public UserModel? CurrentUser { get; set; }
    public string? SessionToken { get; set; }
}
