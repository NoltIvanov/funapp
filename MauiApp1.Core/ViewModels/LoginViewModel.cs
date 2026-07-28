using MauiApp1.Services;

namespace MauiApp1.ViewModels
{
    public class LoginViewModel : BaseViewModel
    {
        private readonly INavigationService _navigationService;
        private readonly IAuthenticationService _authenticationService;
        private readonly IDialogService _dialogService;

        public AsyncCommand LoginWithGoogleCommand { get; }
        public AsyncCommand LoginWithMicrosoftCommand { get; }
        public AsyncCommand LoginWithAppleCommand { get; }
        public bool IsAppleSignInAvailable => _authenticationService.IsAppleSignInAvailable;

        public LoginViewModel(
            INavigationService navigationService,
            IAuthenticationService authenticationService,
            IDialogService dialogService)
        {
            _navigationService = navigationService;
            _authenticationService = authenticationService;
            _dialogService = dialogService;

            LoginWithGoogleCommand = CreateSignInCommand(
                _authenticationService.SignInWithGoogleAsync,
                "Google");
            LoginWithMicrosoftCommand = CreateSignInCommand(
                _authenticationService.SignInWithMicrosoftAsync,
                "Microsoft");
            LoginWithAppleCommand = CreateSignInCommand(
                _authenticationService.SignInWithAppleAsync,
                "Apple");
        }

        private AsyncCommand CreateSignInCommand(Func<Task> signIn, string providerName)
        {
            return new AsyncCommand(
                async () => await SignInAsync(signIn, providerName),
                () => !IsBusy);
        }

        private async Task SignInAsync(Func<Task> signIn, string providerName)
        {
            if (IsBusy) return;

            try
            {
                SetBusy(true);
                await signIn();

                await _navigationService.GoToMainAsync();
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception ex)
            {
                await _dialogService.ShowAlertAsync("Error", $"{providerName} sign-in failed: {ex.Message}", "OK");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SetBusy(bool isBusy)
        {
            IsBusy = isBusy;

            LoginWithGoogleCommand.ChangeCanExecute();
            LoginWithMicrosoftCommand.ChangeCanExecute();
            LoginWithAppleCommand.ChangeCanExecute();
        }
    }
}
