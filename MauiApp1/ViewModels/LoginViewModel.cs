using System.Windows.Input;
using MauiApp1.Models;
using Microsoft.Maui.Authentication;

namespace MauiApp1.ViewModels
{
    public class LoginViewModel : BaseViewModel
    {
        private const string GoogleClientId = "675539284871-54atsjtqdpb0soje89qbvu077vineafp.apps.googleusercontent.com";
        private const string MicrosoftClientId = "84a5b2c7-cc24-4bc2-a272-33bf88b7a0f4";

        private static readonly Uri GoogleAuthUrl = new("https://accounts.google.com/o/oauth2/v2/auth");
        private static readonly Uri GoogleTokenUrl = new("https://oauth2.googleapis.com/token");
        private static readonly Uri MicrosoftAuthUrl = new("https://login.microsoftonline.com/common/oauth2/v2.0/authorize");
        private static readonly Uri MicrosoftTokenUrl = new("https://login.microsoftonline.com/common/oauth2/v2.0/token");

        private const string GoogleRedirectUri = "http://localhost";
        private const string MicrosoftRedirectUri = "msauth://com.wolfapp/d8KUJIGjAISv24pyqyv1QXT%2Fe64%3D";

        private const string GoogleScopes = "openid profile email";
        private const string MicrosoftScopes = "openid profile email User.Read";

        public ICommand LoginWithGoogleCommand { get; }
        public ICommand LoginWithMicrosoftCommand { get; }

        public Func<UserModel, Task>? OnLoginSuccess { get; set; }

        public LoginViewModel()
        {
            LoginWithGoogleCommand = new Command(async () => await LoginWithGoogleAsync());
            LoginWithMicrosoftCommand = new Command(async () => await LoginWithMicrosoftAsync());
        }

        private async Task LoginWithGoogleAsync()
        {
            if (IsBusy) return;

            try
            {
                IsBusy = true;

                var authOptions = new WebAuthenticatorOptions
                {
                    Url = BuildAuthUrl(GoogleAuthUrl, GoogleClientId, GoogleRedirectUri, GoogleScopes),
                    CallbackUrl = new Uri(GoogleRedirectUri),
                    PrefersEphemeralWebBrowserSession = true
                };

#pragma warning disable CA1416
                var result = await WebAuthenticator.AuthenticateAsync(authOptions);
#pragma warning restore CA1416

                var user = new UserModel
                {
                    Provider = "Google",
                    Name = result.Properties.ContainsKey("name") ? result.Properties["name"] : "Google User",
                    Email = result.Properties.ContainsKey("email") ? result.Properties["email"] : string.Empty,
                };

                if (OnLoginSuccess != null)
                    await OnLoginSuccess.Invoke(user);
            }
            catch (TaskCanceledException)
            {
                // User cancelled
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Error", $"Google sign-in failed: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoginWithMicrosoftAsync()
        {
            if (IsBusy) return;

            try
            {
                IsBusy = true;

                var authOptions = new WebAuthenticatorOptions
                {
                    Url = BuildAuthUrl(MicrosoftAuthUrl, MicrosoftClientId, MicrosoftRedirectUri, MicrosoftScopes),
                    CallbackUrl = new Uri(MicrosoftRedirectUri),
                    PrefersEphemeralWebBrowserSession = true
                };

#pragma warning disable CA1416
                var result = await WebAuthenticator.AuthenticateAsync(authOptions);
#pragma warning restore CA1416

                var user = new UserModel
                {
                    Provider = "Microsoft",
                    Name = result.Properties.ContainsKey("name") ? result.Properties["name"] : "Microsoft User",
                    Email = result.Properties.ContainsKey("email") ? result.Properties["email"] : string.Empty,
                };

                if (OnLoginSuccess != null)
                    await OnLoginSuccess.Invoke(user);
            }
            catch (TaskCanceledException)
            {
                // User cancelled
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Error", $"Microsoft sign-in failed: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private static Uri BuildAuthUrl(Uri authority, string clientId, string redirectUri, string scopes)
        {
            var queryString = $"client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&response_type=code&scope={Uri.EscapeDataString(scopes)}&response_mode=query";
            return new Uri($"{authority}?{queryString}");
        }
    }
}
