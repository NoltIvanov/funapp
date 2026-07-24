using MauiApp1.Models;

#if IOS || MACCATALYST
using AuthenticationServices;
using Foundation;
using UIKit;
#endif

namespace MauiApp1.Services;

public class AppleAuthenticationService : IAppleAuthenticationService
{
    public bool IsAvailable
    {
        get
        {
#if IOS || MACCATALYST
            return true;
#else
            return false;
#endif
        }
    }

#if IOS || MACCATALYST
    public Task<UserModel> SignInAsync()
    {
        var completion = new TaskCompletionSource<UserModel>();
        var appleIdProvider = new ASAuthorizationAppleIdProvider();
        var request = appleIdProvider.CreateRequest();
        request.RequestedScopes = new[] { ASAuthorizationScope.FullName, ASAuthorizationScope.Email };

        var controller = new ASAuthorizationController(new[] { request });
        var appleDelegate = new AppleAuthorizationDelegate(completion);
        controller.Delegate = appleDelegate;
        controller.PresentationContextProvider = new ApplePresentationContextProvider();
        controller.PerformRequests();

        return completion.Task.ContinueWith(task =>
        {
            appleDelegate.Dispose();
            return task.GetAwaiter().GetResult();
        }, TaskScheduler.Default);
    }

    private sealed class AppleAuthorizationDelegate : ASAuthorizationControllerDelegate
    {
        private readonly TaskCompletionSource<UserModel> _completion;

        public AppleAuthorizationDelegate(TaskCompletionSource<UserModel> completion)
        {
            _completion = completion;
        }

        public override void DidComplete(ASAuthorizationController controller, ASAuthorization authorization)
        {
            if (authorization.GetCredential<ASAuthorizationAppleIdCredential>() is not ASAuthorizationAppleIdCredential credential)
            {
                _completion.TrySetException(new InvalidOperationException("Apple did not return an Apple ID credential."));
                return;
            }

            _completion.TrySetResult(new UserModel
            {
                Provider = "Apple",
                Id = credential.User,
                Name = FormatFullName(credential.FullName),
                Email = credential.Email ?? string.Empty,
            });
        }

        public override void DidComplete(ASAuthorizationController controller, NSError error)
        {
            _completion.TrySetException(new InvalidOperationException(error.LocalizedDescription));
        }

        private static string FormatFullName(NSPersonNameComponents? name)
        {
            if (name is null)
                return string.Empty;

            var parts = new[] { name.GivenName, name.FamilyName }
                .Where(part => !string.IsNullOrWhiteSpace(part));

            return string.Join(" ", parts);
        }
    }

    private sealed class ApplePresentationContextProvider : NSObject, IASAuthorizationControllerPresentationContextProviding
    {
        public UIWindow GetPresentationAnchor(ASAuthorizationController controller)
        {
            var windowScene = UIApplication.SharedApplication.ConnectedScenes
                .OfType<UIWindowScene>()
                .FirstOrDefault(scene => scene.ActivationState == UISceneActivationState.ForegroundActive)
                ?? UIApplication.SharedApplication.ConnectedScenes
                    .OfType<UIWindowScene>()
                    .FirstOrDefault();

            return windowScene?.Windows.FirstOrDefault(window => window.IsKeyWindow)
                ?? windowScene?.Windows.FirstOrDefault()
                ?? throw new InvalidOperationException("Unable to find the current Apple sign-in window.");
        }
    }
#else
    public Task<UserModel> SignInAsync()
    {
        throw new NotSupportedException("Apple sign-in is not configured for this platform.");
    }
#endif
}
