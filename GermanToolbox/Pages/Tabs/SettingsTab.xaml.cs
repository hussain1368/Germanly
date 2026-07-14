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
        private readonly AutoBackupService autoBackupService;
        private readonly ImageSource? backupButtonImageSource;
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
            autoBackupService = AppServices.GetRequiredService<AutoBackupService>();
            backupButtonImageSource = BackupButton.ImageSource;
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
                AutoBackupSwitch.IsToggled = settingsService.AutoBackupEnabled;
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

        private async void OnAutoBackupToggled(object sender, ToggledEventArgs e)
        {
            if (isRefreshingValues)
            {
                return;
            }

            if (e.Value && !googleAuthService.IsSignedIn)
            {
                isRefreshingValues = true;
                try
                {
                    AutoBackupSwitch.IsToggled = false;
                    settingsService.AutoBackupEnabled = false;
                }
                finally
                {
                    isRefreshingValues = false;
                }

                await Shell.Current.DisplayAlert(
                    "Google account required",
                    "Google must be set up first before automatic backups can be enabled.",
                    "OK");
                return;
            }

            settingsService.AutoBackupEnabled = e.Value;
            autoBackupService.RefreshBackgroundSchedule();
        }

        private async void OnUserGuideTapped(object sender, TappedEventArgs e) =>
            await Shell.Current.GoToAsync(nameof(UserGuidePage));

        private async void OnResetFirstRunGuideClicked(object sender, EventArgs e)
        {
            // TEMP: remove after first-run guide testing
            settingsService.HasSeenUserGuide = false;
            await Shell.Current.DisplayAlert(
                "First-run guide reset",
                "The first-run user guide flag was cleared. Restart the app to see the guide on startup.",
                "OK");
        }

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
                ApplyGoogleAccountState();
            }
        }

        private void OnCancelGoogleSignInClicked(object sender, EventArgs e) =>
            googleAuthService.CancelPendingSignIn();

        private async void OnBackupClicked(object sender, EventArgs e)
        {
            if (!await EnsureGoogleAccountReadyAsync("backup or restore your data"))
            {
                return;
            }

            SetBackupButtonBusyState(true);
            try
            {
                BackupButton.Text = "Preparing backup...";
                await Task.Yield();
                var progress = new Progress<int>(p => BackupButton.Text = p < 100 ? $"Backup {p}%" : "Uploading...");
                await driveBackupService.CreateAndUploadBackupAsync(progress);
                try
                {
                    await driveBackupService.DeleteOlderBackupsAsync();
                }
                catch
                {
                    // Backup already succeeded; automatic cleanup will try again later.
                }

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

        private async void OnRestoreClicked(object sender, EventArgs e)
        {
            if (!await EnsureGoogleAccountReadyAsync("restore a backup"))
            {
                return;
            }

            RestoreRequested?.Invoke(this, EventArgs.Empty);
        }

        private async Task<bool> EnsureGoogleAccountReadyAsync(string actionDescription)
        {
            if (googleAuthService.IsSignedIn)
            {
                return true;
            }

            await Shell.Current.DisplayAlert(
                "Google account required",
                $"Google must be set up first before you can {actionDescription}.",
                "OK");
            return false;
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

            if (currentUser is null && settingsService.AutoBackupEnabled)
            {
                settingsService.AutoBackupEnabled = false;
                AutoBackupSwitch.IsToggled = false;
            }
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
            BackupButton.ImageSource = isBusy ? null : backupButtonImageSource;
            BackupActivityIndicator.IsVisible = isBusy;
            BackupActivityIndicator.IsRunning = isBusy;
        }
    }
}
