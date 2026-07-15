using Android.App;
using Android.Content.PM;
using Microsoft.Maui.Authentication;

namespace MauiApp1
{
    [Activity(Exported = true, LaunchMode = LaunchMode.SingleTop)]
    [IntentFilter(
        new[] { Android.Content.Intent.ActionView },
        Categories = new[] { Android.Content.Intent.CategoryDefault, Android.Content.Intent.CategoryBrowsable },
        DataScheme = "com.googleusercontent.apps.675539284871-bveflltfclied1o4gpebq1ne03n10km7.apps.googleusercontent.com",
        DataPathPrefix = "/oauth2callback")]
    [IntentFilter(
        new[] { Android.Content.Intent.ActionView },
        Categories = new[] { Android.Content.Intent.CategoryDefault, Android.Content.Intent.CategoryBrowsable },
        DataScheme = "msauth")]
    public class AppWebAuthCallbackActivity : WebAuthenticatorCallbackActivity
    {
    }
}
