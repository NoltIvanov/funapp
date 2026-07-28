using MauiApp1.Models;

#if IOS || MACCATALYST
using AuthenticationServices;
using Foundation;
using UIKit;
#endif

namespace MauiApp1.Services;

public class AppleAuthenticationService : IAppleAuthenticationService
{
#if IOS || MACCATALYST
    private ASAuthorizationController? _authorizationController;
    private AppleAuthorizationDelegate? _authorizationDelegate;
    private ApplePresentationContextProvider? _presentationContextProvider;
#endif

    public bool IsAvailable
    {
        get
        {
#if APPLE_SIGN_IN
            return true;
#else
            return false;
#endif
        }
    }

#if IOS || MACCATALYST
    public Task<UserModel> SignInAsync()
    {
        if (_authorizationController is not null)
            throw new InvalidOperationException("Apple sign-in is already in progress.");

        var completion = new TaskCompletionSource<UserModel>(TaskCreationOptions.RunContinuationsAsynchronously);
        var appleIdProvider = new ASAuthorizationAppleIdProvider();
        var request = appleIdProvider.CreateRequest();
        request.RequestedScopes = new[] { ASAuthorizationScope.FullName, ASAuthorizationScope.Email };

        _authorizationDelegate = new AppleAuthorizationDelegate(completion);
        _presentationContextProvider = new ApplePresentationContextProvider();
        _authorizationController = new ASAuthorizationController(new[] { request });
        _authorizationController.Delegate = _authorizationDelegate;
        _authorizationController.PresentationContextProvider = _presentationContextProvider;
        _authorizationController.PerformRequests();

        return completion.Task.ContinueWith(task =>
        {
            ClearAuthorizationState();
            return task.GetAwaiter().GetResult();
        }, TaskScheduler.Default);
    }

    private void ClearAuthorizationState()
    {
        _authorizationController?.Dispose();
        _authorizationController = null;
        _authorizationDelegate?.Dispose();
        _authorizationDelegate = null;
        _presentationContextProvider?.Dispose();
        _presentationContextProvider = null;
    }

    private sealed class AppleAuthorizationDelegate : ASAuthorizationControllerDelegate
    {
        private const long AuthorizationErrorUnknown = 1000;
        private const long AuthorizationErrorCanceled = 1001;
        private const string AuthorizationErrorDomain = "com.apple.AuthenticationServices.AuthorizationError";

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
            if (IsAuthorizationError(error, AuthorizationErrorCanceled))
            {
                _completion.TrySetCanceled();
                return;
            }

            _completion.TrySetException(new InvalidOperationException(CreateAuthorizationErrorMessage(error)));
        }

        private static string FormatFullName(NSPersonNameComponents? name)
        {
            if (name is null)
                return string.Empty;

            var parts = new[] { name.GivenName, name.FamilyName }
                .Where(part => !string.IsNullOrWhiteSpace(part));

            return string.Join(" ", parts);
        }

        private static bool IsAuthorizationError(NSError error, long code)
        {
            return error.Domain == AuthorizationErrorDomain && (long)error.Code == code;
        }

        private static string CreateAuthorizationErrorMessage(NSError error)
        {
            if (!IsAuthorizationError(error, AuthorizationErrorUnknown))
                return error.LocalizedDescription;

            var bundleId = NSBundle.MainBundle.BundleIdentifier ?? "the app bundle id";
            return $"{error.LocalizedDescription} Verify that Sign in with Apple is enabled for {bundleId} and that this build is signed with a matching provisioning profile.";
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
