namespace MauiApp1.Services;

public interface IAuthenticationService
{
    bool IsAppleSignInAvailable { get; }

    Task SignInWithGoogleAsync();
    Task SignInWithMicrosoftAsync();
    Task SignInWithAppleAsync();
}
