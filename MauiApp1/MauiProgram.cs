using MauiApp1.Services;
using MauiApp1.ViewModels;
using MauiApp1.Views;
using Microsoft.Extensions.Logging;

namespace MauiApp1
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            builder.Services.AddSingleton<INavigationService, NavigationService>();
            builder.Services.AddSingleton<IUserSessionService, UserSessionService>();
            builder.Services.AddSingleton<IBrowserAuthenticationService, BrowserAuthenticationService>();
            builder.Services.AddSingleton<IAppleAuthenticationService, AppleAuthenticationService>();

            builder.Services.AddTransient<LoginViewModel>();
            builder.Services.AddTransient<FeedViewModel>();
            builder.Services.AddTransient<SettingsViewModel>();

            builder.Services.AddTransient<LoginView>();
            builder.Services.AddTransient<FeedPage>();
            builder.Services.AddTransient<SettingsPage>();
            builder.Services.AddTransient<MainTabbedPage>();
            builder.Services.AddTransient<AppShell>();

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
