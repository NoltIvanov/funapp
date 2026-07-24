namespace MauiApp1.Services;

public class NavigationService : INavigationService
{
    public async Task GoToMainAsync()
    {
        await Shell.Current.GoToAsync("//Main");
    }

    public async Task GoToLoginAsync()
    {
        await Shell.Current.GoToAsync("//Login");
    }
}
