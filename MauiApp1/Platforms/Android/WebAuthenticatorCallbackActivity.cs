using Android.App;
using Android.Content.PM;
using Microsoft.Maui.Authentication;

namespace MauiApp1
{
    [Activity(Exported = true, LaunchMode = LaunchMode.SingleTop)]
    [IntentFilter(
        new[] { Android.Content.Intent.ActionView },
        Categories = new[] { Android.Content.Intent.CategoryDefault, Android.Content.Intent.CategoryBrowsable },
        DataScheme = "com.googleusercontent.apps.941430168156-87e8flf4huf7sr66foc61kotbb30lqtn",
        DataPathPrefix = "/oauth2callback")]
    [IntentFilter(
        new[] { Android.Content.Intent.ActionView },
        Categories = new[] { Android.Content.Intent.CategoryDefault, Android.Content.Intent.CategoryBrowsable },
        DataScheme = "msauth")]
    public class AppWebAuthCallbackActivity : WebAuthenticatorCallbackActivity
    {
    }
}
