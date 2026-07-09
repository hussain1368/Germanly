namespace GermanToolbox
{
    public interface IGoogleNativeSignInService
    {
        bool IsSupported { get; }

        Task<GoogleSignedInUser> SignInAsync();

        Task<string> GetAccessTokenAsync(
            string email,
            string? existingAccessToken,
            bool forceRefresh);

        Task SignOutAsync();
    }
}