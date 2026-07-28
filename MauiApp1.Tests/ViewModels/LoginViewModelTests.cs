using MauiApp1.ViewModels;

namespace MauiApp1.Tests.ViewModels;

public class LoginViewModelTests
{
    [Fact]
    public async Task GoogleCommand_SetsBusyDisablesCommandsAndNavigatesAfterSuccessfulSignIn()
    {
        var navigationService = new RecordingNavigationService();
        var authenticationService = new StubAuthenticationService();
        var dialogService = new RecordingDialogService();
        var signInStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSignIn = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        authenticationService.SignInWithGoogleAsyncHandler = async () =>
        {
            signInStarted.SetResult();
            await releaseSignIn.Task;
        };
        var viewModel = new LoginViewModel(navigationService, authenticationService, dialogService);

        var execution = viewModel.LoginWithGoogleCommand.ExecuteAsync();
        await signInStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.True(viewModel.IsBusy);
        Assert.False(viewModel.LoginWithGoogleCommand.CanExecute(null));
        Assert.False(viewModel.LoginWithMicrosoftCommand.CanExecute(null));
        Assert.False(viewModel.LoginWithAppleCommand.CanExecute(null));

        releaseSignIn.SetResult();
        await execution.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.False(viewModel.IsBusy);
        Assert.True(viewModel.LoginWithGoogleCommand.CanExecute(null));
        Assert.Equal(1, navigationService.GoToMainCalls);
        Assert.Empty(dialogService.Alerts);
    }

    [Fact]
    public async Task MicrosoftCommand_ShowsDialogAndDoesNotNavigateWhenSignInFails()
    {
        var navigationService = new RecordingNavigationService();
        var authenticationService = new StubAuthenticationService
        {
            SignInWithMicrosoftAsyncHandler = () => throw new InvalidOperationException("boom"),
        };
        var dialogService = new RecordingDialogService();
        var viewModel = new LoginViewModel(navigationService, authenticationService, dialogService);

        await viewModel.LoginWithMicrosoftCommand.ExecuteAsync();

        Assert.False(viewModel.IsBusy);
        Assert.Equal(0, navigationService.GoToMainCalls);
        var alert = Assert.Single(dialogService.Alerts);
        Assert.Equal("Error", alert.Title);
        Assert.Equal("Microsoft sign-in failed: boom", alert.Message);
        Assert.Equal("OK", alert.Cancel);
    }

    [Fact]
    public async Task AppleCommand_IgnoresCancellationWithoutShowingDialogOrNavigating()
    {
        var navigationService = new RecordingNavigationService();
        var authenticationService = new StubAuthenticationService
        {
            SignInWithAppleAsyncHandler = () => throw new TaskCanceledException(),
        };
        var dialogService = new RecordingDialogService();
        var viewModel = new LoginViewModel(navigationService, authenticationService, dialogService);

        await viewModel.LoginWithAppleCommand.ExecuteAsync();

        Assert.False(viewModel.IsBusy);
        Assert.Equal(0, navigationService.GoToMainCalls);
        Assert.Empty(dialogService.Alerts);
    }

    [Fact]
    public void IsAppleSignInAvailable_DelegatesToAuthenticationService()
    {
        var viewModel = new LoginViewModel(
            new RecordingNavigationService(),
            new StubAuthenticationService { IsAppleSignInAvailable = true },
            new RecordingDialogService());

        Assert.True(viewModel.IsAppleSignInAvailable);
    }
}
