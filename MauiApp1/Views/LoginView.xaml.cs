using MauiApp1.Services;
using MauiApp1.ViewModels;

namespace MauiApp1.Views;

public partial class LoginView : ContentPage
{
    public LoginView(INavigationService navigationService)
    {
        InitializeComponent();
        BindingContext = new LoginViewModel(navigationService);
    }
}
