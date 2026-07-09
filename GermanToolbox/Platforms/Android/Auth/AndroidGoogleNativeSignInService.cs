using Android.App;
using Android.Content;
using Android.Gms.Auth;
using Android.Gms.Auth.Api.SignIn;
using Android.Gms.Common;
using Android.Gms.Common.Apis;
using Android.Gms.Tasks;
using Android.Runtime;
using Microsoft.Maui.ApplicationModel;
using Task = System.Threading.Tasks.Task;
using TaskCompletionSource = System.Threading.Tasks.TaskCompletionSource;

namespace GermanToolbox
{
    public sealed class AndroidGoogleNativeSignInService : Java.Lang.Object, IGoogleNativeSignInService
    {
        private const int SignInRequestCode = 43173;
        private const string DriveAppDataScope = "https://www.googleapis.com/auth/drive.appdata";
        private static TaskCompletionSource<GoogleSignedInUser>? pendingSignIn;
        private GoogleSignInClient? signInClient;

        public bool IsSupported => true;

        public Task<GoogleSignedInUser> SignInAsync()
        {
            var activity = Platform.CurrentActivity
                ?? throw new InvalidOperationException("No active Android activity is available for Google sign-in.");

            var playServicesStatus = GoogleApiAvailability.Instance.IsGooglePlayServicesAvailable(activity);
            if (playServicesStatus != ConnectionResult.Success)
            {
                throw new InvalidOperationException(
                    $"Google Play Services is unavailable on this device. Status code: {playServicesStatus}.");
            }

            if (pendingSignIn is not null && !pendingSignIn.Task.IsCompleted)
            {
                throw new InvalidOperationException("A Google sign-in request is already in progress.");
            }

            var options = new GoogleSignInOptions.Builder(GoogleSignInOptions.DefaultSignIn)
                .RequestScopes(new Scope(DriveAppDataScope))
                .RequestEmail()
                .Build();

            signInClient = GoogleSignIn.GetClient(activity, options);
            pendingSignIn = new TaskCompletionSource<GoogleSignedInUser>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            activity.StartActivityForResult(signInClient.SignInIntent, SignInRequestCode);
            return pendingSignIn.Task;
        }

        public async Task<string> GetAccessTokenAsync(
            string email,
            string? existingAccessToken,
            bool forceRefresh)
        {
            var activity = Platform.CurrentActivity
                ?? throw new InvalidOperationException("No active Android activity is available for Google Drive access.");
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new InvalidOperationException("Google Drive access failed: no signed-in Google account is available.");
            }

            var scope = $"oauth2:{DriveAppDataScope}";
            try
            {
                return await System.Threading.Tasks.Task.Run(() =>
                {
                    if (forceRefresh && !string.IsNullOrWhiteSpace(existingAccessToken))
                    {
                        GoogleAuthUtil.ClearToken(activity, existingAccessToken);
                    }

                    return GoogleAuthUtil.GetToken(activity, email, scope);
                });
            }
            catch (UserRecoverableAuthException ex)
            {
                throw new InvalidOperationException(
                    "Google Drive access needs permission. Sign out of Google, sign in again, and approve Drive backup access.",
                    ex);
            }
        }

        public Task SignOutAsync()
        {
            var activity = Platform.CurrentActivity;
            if (activity is null)
            {
                return Task.CompletedTask;
            }

            signInClient ??= GoogleSignIn.GetClient(
                activity,
                new GoogleSignInOptions.Builder(GoogleSignInOptions.DefaultSignIn)
                    .RequestEmail()
                    .Build());

            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            signInClient.SignOut().AddOnCompleteListener(new CompletionListener(
                onSuccess: () => completion.TrySetResult(),
                onFailure: exception => completion.TrySetException(exception)));
            return completion.Task;
        }

        internal static bool TryHandleActivityResult(int requestCode, Result resultCode, Intent? data)
        {
            if (requestCode != SignInRequestCode || pendingSignIn is null)
            {
                return false;
            }

            if (data is null)
            {
                pendingSignIn.TrySetException(new InvalidOperationException("Google sign-in was cancelled."));
                pendingSignIn = null;
                return true;
            }

            var signInTask = GoogleSignIn.GetSignedInAccountFromIntent(data);
            signInTask.AddOnCompleteListener(new CompletionListener(
                onSuccess: () =>
                {
                    try
                    {
                        var account = signInTask.Result?.JavaCast<GoogleSignInAccount>();
                        var email = account?.Email;
                        if (string.IsNullOrWhiteSpace(email))
                        {
                            throw new InvalidOperationException(
                                "Google sign-in did not return an email address.");
                        }

                        var displayName = string.IsNullOrWhiteSpace(account?.DisplayName)
                            ? email
                            : account.DisplayName;
                        var firstName = GetFirstName(displayName, email);
                        var photoUrl = account?.PhotoUrl?.ToString();
                        pendingSignIn?.TrySetResult(
                            new GoogleSignedInUser(email, displayName, firstName, photoUrl));
                    }
                    catch (Exception ex)
                    {
                        pendingSignIn?.TrySetException(ex);
                    }
                    finally
                    {
                        pendingSignIn = null;
                    }
                },
                onFailure: exception =>
                {
                    pendingSignIn?.TrySetException(exception);
                    pendingSignIn = null;
                }));

            return true;
        }

        private sealed class CompletionListener : Java.Lang.Object, IOnCompleteListener
        {
            private readonly Action onSuccess;
            private readonly Action<Exception> onFailure;

            public CompletionListener(Action onSuccess, Action<Exception> onFailure)
            {
                this.onSuccess = onSuccess;
                this.onFailure = onFailure;
            }

            public void OnComplete(global::Android.Gms.Tasks.Task task)
            {
                if (task.IsSuccessful)
                {
                    onSuccess();
                    return;
                }

                onFailure(CreateFailureException(task.Exception));
            }

            private static Exception CreateFailureException(Exception? exception)
            {
                if (exception is ApiException apiException)
                {
                    return apiException.StatusCode switch
                    {
                        10 => new InvalidOperationException(
                            "Google sign-in failed because the Android OAuth configuration does not match this app. Verify the package name and SHA-1 fingerprint in Google Cloud for com.hussain.germanly."),
                        7 => new InvalidOperationException(
                            "Google sign-in failed because the device could not reach Google services. Check the network connection and try again."),
                        12501 => new InvalidOperationException("Google sign-in was cancelled."),
                        12500 => new InvalidOperationException(
                            "Google sign-in failed on this device. Try again or choose a different Google account."),
                        _ => new InvalidOperationException(
                            $"Google sign-in failed with status code {apiException.StatusCode}: {apiException.Status.StatusMessage ?? apiException.Message}")
                    };
                }

                return exception is null
                    ? new InvalidOperationException("Google sign-in failed.")
                    : new InvalidOperationException(exception.Message ?? "Google sign-in failed.");
            }
        }

        private static string GetFirstName(string? displayName, string email)
        {
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                var firstPart = displayName
                    .Trim()
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(firstPart))
                {
                    return firstPart;
                }
            }

            var atIndex = email.IndexOf('@');
            return atIndex > 0 ? email[..atIndex] : email;
        }
    }
}