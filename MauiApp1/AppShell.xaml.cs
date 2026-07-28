using MauiApp1.Services;

namespace MauiApp1
{
    public partial class AppShell : Shell
    {
        private readonly INavigationService _navigationService;
        private readonly IUserSessionService _userSessionService;

        public AppShell(
            INavigationService navigationService,
            IUserSessionService userSessionService)
        {
            _navigationService = navigationService;
            _userSessionService = userSessionService;

            InitializeComponent();

            Loaded += OnLoaded;
        }

        private async void OnLoaded(object? sender, EventArgs e)
        {
            Loaded -= OnLoaded;

            await _userSessionService.LoadSessionAsync();
            if (_userSessionService.CurrentUser is not null)
                await _navigationService.GoToMainAsync();
        }
    }
}
