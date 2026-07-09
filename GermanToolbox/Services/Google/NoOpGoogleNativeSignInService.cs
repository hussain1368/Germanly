namespace GermanToolbox
{
    public sealed class NoOpGoogleNativeSignInService : IGoogleNativeSignInService
    {
        public bool IsSupported => false;

        public Task<GoogleSignedInUser> SignInAsync() =>
            Task.FromException<GoogleSignedInUser>(
                new NotSupportedException("Native Google sign-in is not supported on this platform."));

        public Task<string> GetAccessTokenAsync(
            string email,
            string? existingAccessToken,
            bool forceRefresh) =>
            Task.FromException<string>(
                new NotSupportedException("Native Google access tokens are not supported on this platform."));

        public Task SignOutAsync() => Task.CompletedTask;
    }
}