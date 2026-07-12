namespace GermanToolbox
{
    public partial class SettingsTab : ContentView
    {
        private const int MinimumLearnedThreshold = 1;
        private const int MaximumLearnedThreshold = 20;
        private const int MinimumChunkSize = 1;
        private const int MaximumChunkSize = 100;
        private const int ChunkSizeStep = 5;

        private readonly PracticeSettingsService settingsService;
        private readonly GoogleAuthService googleAuthService;
        private readonly DriveBackupService driveBackupService;
        private bool isRefreshingValues;
        private bool isGoogleActionBusy;
        private bool isBackupActionBusy;
        private static readonly Color BackupEnabledBackgroundColor = Color.FromArgb("#2563EB");
        private static readonly Color BackupEnabledTextColor = Color.FromArgb("#FFFFFF");
        private static readonly Color BackupDisabledBackgroundColor = Color.FromArgb("#D8E1EE");
        private static readonly Color BackupDisabledTextColor = Color.FromArgb("#7A8797");

        public SettingsTab()
        {
            InitializeComponent();
            settingsService = AppServices.GetRequiredService<PracticeSettingsService>();
            googleAuthService = AppServices.GetRequiredService<GoogleAuthService>();
            driveBackupService = AppServices.GetRequiredService<DriveBackupService>();
            googleAuthService.AuthenticationStateChanged += OnAuthenticationStateChanged;
            RefreshValues();
        }

        public event EventHandler? ClearProgressRequested;

        public event EventHandler? ProgressCriteriaChanged;

        public event EventHandler? RestoreRequested;

        public void RefreshValues()
        {
            isRefreshingValues = true;
            try
            {
                settingsService.LearnedThreshold = Math.Clamp(
                    settingsService.LearnedThreshold,
                    MinimumLearnedThreshold,
                    MaximumLearnedThreshold);
                settingsService.TestChunkSize = Math.Clamp(
                    settingsService.TestChunkSize,
                    MinimumChunkSize,
                    MaximumChunkSize);

                LearnedThresholdValueLabel.Text = settingsService.LearnedThreshold.ToString();
                ChunkSizeValueLabel.Text = settingsService.TestChunkSize.ToString();
                SoundsSwitch.IsToggled = settingsService.SoundsEnabled;
                VibrationsSwitch.IsToggled = settingsService.VibrationsEnabled;
                SetBackupButtonBusyState(isBackupActionBusy);
                ApplyGoogleAccountState();
            }
            finally
            {
                isRefreshingValues = false;
            }
        }

        private void OnDecreaseThresholdTapped(object sender, TappedEventArgs e)
        {
            settingsService.LearnedThreshold = Math.Max(
                MinimumLearnedThreshold,
                settingsService.LearnedThreshold - 1);
            RefreshValues();
            ProgressCriteriaChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnIncreaseThresholdTapped(object sender, TappedEventArgs e)
        {
            settingsService.LearnedThreshold = Math.Min(
                MaximumLearnedThreshold,
                settingsService.LearnedThreshold + 1);
            RefreshValues();
            ProgressCriteriaChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnDecreaseChunkSizeTapped(object sender, TappedEventArgs e)
        {
            settingsService.TestChunkSize = Math.Max(
                MinimumChunkSize,
                settingsService.TestChunkSize - ChunkSizeStep);
            RefreshValues();
        }

        private void OnIncreaseChunkSizeTapped(object sender, TappedEventArgs e)
        {
            settingsService.TestChunkSize = Math.Min(
                MaximumChunkSize,
                settingsService.TestChunkSize + ChunkSizeStep);
            RefreshValues();
        }

        private void OnSoundsToggled(object sender, ToggledEventArgs e)
        {
            if (!isRefreshingValues)
            {
                settingsService.SoundsEnabled = e.Value;
            }
        }

        private void OnVibrationsToggled(object sender, ToggledEventArgs e)
        {
            if (!isRefreshingValues)
            {
                settingsService.VibrationsEnabled = e.Value;
            }
        }

        private async void OnUserGuideTapped(object sender, TappedEventArgs e) =>
            await Shell.Current.GoToAsync(nameof(UserGuidePage));

        private void OnClearProgressClicked(object sender, EventArgs e) =>
            ClearProgressRequested?.Invoke(this, EventArgs.Empty);

        private async void OnGoogleActionClicked(object sender, EventArgs e)
        {
            if (googleAuthService.IsSignedIn)
            {
                var shouldSignOut = await Shell.Current.DisplayAlert(
                    "Sign out",
                    "Do you want to sign out of Google?",
                    "Sign out",
                    "Cancel");

                if (!shouldSignOut)
                {
                    return;
                }

                await googleAuthService.SignOutAsync();
                ApplyGoogleAccountState();
                return;
            }

            SetGoogleSignInOverlayVisible(true);
            GoogleActionButton.IsEnabled = false;
            var previousText = GoogleActionButton.Text;
            GoogleActionButton.Text = "Connecting...";
            isGoogleActionBusy = true;

            try
            {
                await googleAuthService.SignInAsync();
                ApplyGoogleAccountState();
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert(
                    "Google sign-in failed",
                    ex.Message,
                    "OK");
            }
            finally
            {
                isGoogleActionBusy = false;
                SetGoogleSignInOverlayVisible(false);
                GoogleActionButton.Text = previousText;
                GoogleActionButton.IsEnabled = true;
            }
        }

        private void OnCancelGoogleSignInClicked(object sender, EventArgs e) =>
            googleAuthService.CancelPendingSignIn();

        private async void OnBackupClicked(object sender, EventArgs e)
        {
            SetBackupButtonBusyState(true);
            try
            {
                BackupButton.Text = "Preparing backup...";
                await Task.Yield();
                var progress = new Progress<int>(p => BackupButton.Text = p < 100 ? $"Backup {p}%" : "Uploading...");
                var zip = await driveBackupService.CreateBackupZipAsync(progress);
                var fileName = $"germanly_backup_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.zip";
                var fileId = await driveBackupService.UploadBackupAsync(zip, fileName);
                BackupButton.Text = "Backup uploaded successfully";
                if (Shell.Current.CurrentPage is ContentPage page1)
                {
                    await ToastService.ShowAsync(page1, "Backup uploaded to Google Drive");
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Backup failed", ex.Message, "OK");
                BackupButton.Text = "Backup failed";
            }
            finally
            {
                BackupButton.Text = "Backup now";
                SetBackupButtonBusyState(false);
            }
        }

        private void OnRestoreClicked(object sender, EventArgs e)
        {
            RestoreRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ApplyGoogleAccountState()
        {
            var currentUser = googleAuthService.CurrentUser;
            GoogleAccountStatusLabel.Text = currentUser is null
                ? "Not connected. Sign in with Google to prepare Drive backup."
                : $"Connected as {currentUser.Email}";
            GoogleActionButton.Text = currentUser is null ? "Sign in to Google" : "Sign out";
            GoogleActionButton.ImageSource = currentUser is null ? "google.png" : "power.png";
            GoogleActionButton.IsEnabled = !isGoogleActionBusy;
        }

        private void OnAuthenticationStateChanged(object? sender, EventArgs e) =>
            ApplyGoogleAccountState();

        private void SetGoogleSignInOverlayVisible(bool isVisible)
        {
            GoogleSignInOverlay.IsVisible = isVisible;
            GoogleSignInActivityIndicator.IsRunning = isVisible;
            CancelGoogleSignInButton.IsVisible =
                isVisible && DeviceInfo.Current.Platform == DevicePlatform.WinUI;
        }

        private void SetBackupButtonBusyState(bool isBusy)
        {
            isBackupActionBusy = isBusy;
            BackupButton.IsEnabled = !isBusy;
            BackupButton.BackgroundColor = isBusy ? BackupDisabledBackgroundColor : BackupEnabledBackgroundColor;
            BackupButton.TextColor = isBusy ? BackupDisabledTextColor : BackupEnabledTextColor;
        }
    }
}
