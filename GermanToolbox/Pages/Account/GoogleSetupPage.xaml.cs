namespace GermanToolbox
{
    public partial class GoogleSetupPage : ContentPage
    {
        private readonly GoogleAuthService googleAuthService;
        private readonly PracticeSettingsService settingsService;
        private bool isSignInInProgress;

        public GoogleSetupPage()
        {
            InitializeComponent();
            googleAuthService = AppServices.GetRequiredService<GoogleAuthService>();
            settingsService = AppServices.GetRequiredService<PracticeSettingsService>();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            if (isSignInInProgress)
            {
                SetSignInOverlayVisible(true);
            }
        }

        private async void OnSignInClicked(object sender, EventArgs e)
        {
            if (isSignInInProgress)
            {
                return;
            }

            isSignInInProgress = true;
            SignInButton.IsEnabled = false;
            SetSignInOverlayVisible(true);

            try
            {
                await googleAuthService.SignInAsync();
                await CloseSetupAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert(
                    "Google sign-in failed",
                    ex.Message,
                    "OK");
            }
            finally
            {
                isSignInInProgress = false;
                SignInButton.IsEnabled = true;
                SetSignInOverlayVisible(false);
            }
        }

        private async void OnSetupLaterClicked(object sender, TappedEventArgs e) =>
            await CloseSetupAsync();

        private async Task CloseSetupAsync()
        {
            settingsService.HasSeenGoogleSetupPrompt = true;
            await Shell.Current.GoToAsync("..");
        }

        private void SetSignInOverlayVisible(bool isVisible)
        {
            SignInOverlay.IsVisible = isVisible;
            SignInActivityIndicator.IsRunning = isVisible;
            CancelSignInButton.IsVisible =
                isVisible && DeviceInfo.Current.Platform == DevicePlatform.WinUI;
        }

        private void OnCancelSignInClicked(object sender, EventArgs e) =>
            googleAuthService.CancelPendingSignIn();
    }
}
