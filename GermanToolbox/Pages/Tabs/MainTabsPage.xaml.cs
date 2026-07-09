using System.Globalization;
using System.Text;
using Microsoft.Maui.Controls.Shapes;

namespace GermanToolbox
{
    public partial class MainTabsPage : ContentPage, IQueryAttributable
    {
        private readonly TestSessionService testSessionService;
        private readonly DriveBackupService driveBackupService;
        private bool isClearingProgress;
        private bool isClearProgressConfirmed;
        private bool isRestoringBackup;
        private bool isRestoreConfirmed;
        private bool isRestoreSchemaWarningConfirmed;
        private byte[]? pendingRestoreBytes;
        private DriveBackupRestorePlan? pendingRestorePlan;
        private DriveBackupItem? selectedRestoreBackup;
        private List<DriveBackupItem> availableRestoreBackups = [];

        public MainTabsPage()
        {
            InitializeComponent();
            testSessionService = AppServices.GetRequiredService<TestSessionService>();
            driveBackupService = AppServices.GetRequiredService<DriveBackupService>();
            SelectTab("Home", refreshContent: false);
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            SettingsTabContent.RefreshValues();
            await HomeTabContent.RefreshAsync();
            await PracticeTabContent.RefreshStatsAsync();
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("tab", out var tabValue) && tabValue is string tab)
            {
                SelectTab(tab);
            }

            query.Clear();
        }

        private void OnBottomTabSelected(object sender, TabSelectedEventArgs e) =>
            SelectTab(e.Tab);

        private void SelectTab(string selectedTab, bool refreshContent = true)
        {
            PracticeTabContent.IsVisible = selectedTab == "Practice";
            HomeTabContent.IsVisible = selectedTab == "Home";
            SearchTabContent.IsVisible = selectedTab == "Search";
            SettingsTabContent.IsVisible = selectedTab == "Settings";
            BottomNav.SelectedTab = selectedTab;

            if (selectedTab == "Practice" && refreshContent)
            {
                _ = PracticeTabContent.RefreshStatsAsync();
            }
            else if (selectedTab == "Home" && refreshContent)
            {
                _ = HomeTabContent.RefreshAsync();
            }
            else if (selectedTab == "Settings")
            {
                SettingsTabContent.RefreshValues();
            }
        }

        private void OnProgressCriteriaChanged(object? sender, EventArgs e)
        {
            _ = HomeTabContent.RefreshAsync();
            _ = PracticeTabContent.RefreshStatsAsync();
        }

        private async void OnRestoreRequested(object? sender, EventArgs e)
        {
            if (RestoreOverlay.IsVisible)
            {
                return;
            }

            ShowRestoreProgressState(
                "Loading backups...",
                "Please wait while Google Drive backups are loaded.");
            RestoreOverlay.Opacity = 0;
            RestoreOverlay.IsVisible = true;
            await RestoreOverlay.FadeTo(1, 120, Easing.CubicOut);

            try
            {
                await Task.Yield();
                availableRestoreBackups = (await driveBackupService.ListBackupsAsync()).ToList();
                if (availableRestoreBackups.Count == 0)
                {
                    await HideRestoreOverlayAsync();
                    await DisplayAlert("No backups", "No backups found on Google Drive.", "OK");
                    return;
                }

                BuildRestoreBackupList();
                SetRestoreSelectionState(null);
                SetRestoreConfirmationState(false);
                SetRestoreBusyState(false);
            }
            catch (Exception ex)
            {
                await HideRestoreOverlayAsync();
                await DisplayAlert("Restore failed", ex.Message, "OK");
            }
        }

        private async void OnClearProgressRequested(object? sender, EventArgs e)
        {
            if (ClearProgressOverlay.IsVisible)
            {
                return;
            }

            SetClearProgressConfirmationState(false);
            ClearProgressOverlay.Opacity = 0;
            ClearProgressOverlay.IsVisible = true;
            await ClearProgressOverlay.FadeTo(1, 120, Easing.CubicOut);
        }

        private async void OnCancelClearProgressTapped(object sender, TappedEventArgs e) =>
            await HideClearProgressOverlayAsync();

        private void OnClearProgressConfirmationTapped(object sender, TappedEventArgs e) =>
            SetClearProgressConfirmationState(!isClearProgressConfirmed);

        private async void OnConfirmClearProgressTapped(object sender, TappedEventArgs e)
        {
            if (isClearingProgress || !isClearProgressConfirmed)
            {
                return;
            }

            isClearingProgress = true;
            try
            {
                await testSessionService.ResetProgressAsync();
                await HomeTabContent.RefreshAsync();
                await PracticeTabContent.RefreshStatsAsync();
                await HideClearProgressOverlayAsync();
            }
            catch (Exception ex)
            {
                ClearProgressOverlay.IsVisible = false;
                ClearProgressOverlay.Opacity = 1;
                await DisplayAlert("Reset failed", $"Progress was not reset. {ex.Message}", "OK");
            }
            finally
            {
                isClearingProgress = false;
            }
        }

        protected override bool OnBackButtonPressed()
        {
            if (RestoreOverlay.IsVisible)
            {
                if (isRestoringBackup)
                {
                    return true;
                }

                _ = HideRestoreOverlayAsync();
                return true;
            }

            if (ClearProgressOverlay.IsVisible)
            {
                _ = HideClearProgressOverlayAsync();
                return true;
            }

            return base.OnBackButtonPressed();
        }

        private async Task HideClearProgressOverlayAsync()
        {
            if (!ClearProgressOverlay.IsVisible)
            {
                return;
            }

            await ClearProgressOverlay.FadeTo(0, 100, Easing.CubicIn);
            ClearProgressOverlay.IsVisible = false;
            ClearProgressOverlay.Opacity = 1;
            SetClearProgressConfirmationState(false);
        }

        private void SetClearProgressConfirmationState(bool isConfirmed)
        {
            isClearProgressConfirmed = isConfirmed;
            ClearProgressConfirmationBox.BackgroundColor = Color.FromArgb(
                isConfirmed ? "#D43D35" : "#FFFFFF");
            ClearProgressConfirmationBox.Stroke = Color.FromArgb("#D43D35");
            ClearProgressConfirmationCheckMark.IsVisible = isConfirmed;
            ClearProgressConfirmButton.InputTransparent = !isConfirmed;
            ClearProgressConfirmButton.Opacity = isConfirmed ? 1 : 0.6;
            ClearProgressConfirmButton.BackgroundColor = Color.FromArgb(
                isConfirmed ? "#D43D35" : "#E6A8A4");
            ClearProgressConfirmButton.Stroke = Color.FromArgb(
                isConfirmed ? "#D43D35" : "#E6A8A4");
        }

        private void BuildRestoreBackupList()
        {
            RestoreBackupListContainer.Children.Clear();
            for (var index = 0; index < availableRestoreBackups.Count; index++)
            {
                var backup = availableRestoreBackups[index];
                var itemBorder = new Border
                {
                    Padding = new Thickness(12, 10),
                    BackgroundColor = Color.FromArgb("#FFFFFF"),
                    Stroke = Color.FromArgb("#DDE8F5"),
                    StrokeThickness = 1,
                    Margin = new Thickness(0, 0, 0, -1)
                };
                itemBorder.StrokeShape = new RoundRectangle { CornerRadius = 8 };

                var titleLabel = new Label
                {
                    FontAttributes = FontAttributes.Bold,
                    FontFamily = "OpenSansSemibold",
                    FontSize = 14,
                    Text = backup.Modified.LocalDateTime.ToString("f", CultureInfo.CurrentCulture),
                    TextColor = Color.FromArgb("#171717")
                };

                var stack = new VerticalStackLayout { Spacing = 2 };
                stack.Children.Add(titleLabel);

                itemBorder.Content = stack;
                itemBorder.GestureRecognizers.Add(new TapGestureRecognizer
                {
                    Command = new Command(() => SetRestoreSelectionState(backup))
                });

                RestoreBackupListContainer.Children.Add(itemBorder);
            }
        }

        private void SetRestoreSelectionState(DriveBackupItem? selectedBackup)
        {
            selectedRestoreBackup = selectedBackup;

            for (var index = 0; index < RestoreBackupListContainer.Children.Count; index++)
            {
                if (RestoreBackupListContainer.Children[index] is not Border border)
                {
                    continue;
                }

                var isSelected = selectedBackup is not null && ReferenceEquals(availableRestoreBackups[index], selectedBackup);
                border.BackgroundColor = Color.FromArgb(isSelected ? "#EAF1FF" : "#FFFFFF");
                border.Stroke = Color.FromArgb(isSelected ? "#2563EB" : "#DDE8F5");
                border.StrokeThickness = isSelected ? 2 : 1;
            }

            UpdateRestoreConfirmButtonState();
        }

        private void SetRestoreConfirmationState(bool isConfirmed)
        {
            isRestoreConfirmed = isConfirmed;
            RestoreConfirmationBox.BackgroundColor = Color.FromArgb(
                isConfirmed ? "#2563EB" : "#FFFFFF");
            RestoreConfirmationBox.Stroke = Color.FromArgb("#2563EB");
            RestoreConfirmationCheckMark.IsVisible = isConfirmed;
            UpdateRestoreConfirmButtonState();
        }

        private void UpdateRestoreConfirmButtonState()
        {
            var canConfirm = selectedRestoreBackup is not null && isRestoreConfirmed && !isRestoringBackup;
            RestoreConfirmButton.InputTransparent = !canConfirm;
            RestoreConfirmButton.Opacity = canConfirm ? 1 : 0.6;
            RestoreConfirmButton.BackgroundColor = Color.FromArgb(
                canConfirm ? "#2563EB" : "#EAF1FF");
            RestoreConfirmButton.Stroke = Color.FromArgb(
                canConfirm ? "#2563EB" : "#CFE0FF");
            RestoreConfirmLabel.TextColor = Color.FromArgb(
                canConfirm ? "#FFFFFF" : "#2563EB");
        }

        private void SetRestoreBusyState(bool isBusy)
        {
            isRestoringBackup = isBusy;
            RestoreSelectionContent.IsVisible = !isBusy;
            RestoreProgressContent.IsVisible = isBusy;
            RestoreSchemaWarningContent.IsVisible = false;
            RestoreActivityIndicator.IsRunning = isBusy;
            UpdateRestoreConfirmButtonState();
        }

        private void ShowRestoreProgressState(string title, string detail)
        {
            RestoreProgressLabel.Text = title;
            RestoreProgressDetailLabel.Text = detail;
            SetRestoreBusyState(true);
        }

        private void OnRestoreConfirmationTapped(object sender, TappedEventArgs e) =>
            SetRestoreConfirmationState(!isRestoreConfirmed);

        private async void OnCancelRestoreTapped(object sender, TappedEventArgs e) =>
            await HideRestoreOverlayAsync();

        private void OnRestoreSchemaWarningConfirmationTapped(object sender, TappedEventArgs e) =>
            SetRestoreSchemaWarningConfirmationState(!isRestoreSchemaWarningConfirmed);

        private async void OnCancelRestoreSchemaWarningTapped(object sender, TappedEventArgs e) =>
            await HideRestoreOverlayAsync();

        private async void OnConfirmRestoreSchemaWarningTapped(object sender, TappedEventArgs e)
        {
            if (!isRestoreSchemaWarningConfirmed ||
                pendingRestoreBytes is null ||
                pendingRestorePlan is null)
            {
                return;
            }

            await RestoreBackupAsync(pendingRestoreBytes, pendingRestorePlan);
        }

        private async void OnConfirmRestoreTapped(object sender, TappedEventArgs e)
        {
            if (selectedRestoreBackup is null || !isRestoreConfirmed || isRestoringBackup)
            {
                return;
            }

            ShowRestoreProgressState(
                "Restoring backup...",
                selectedRestoreBackup.Modified.LocalDateTime.ToString("f", CultureInfo.CurrentCulture));

            try
            {
                var bytes = await driveBackupService.DownloadBackupAsync(selectedRestoreBackup.Id);
                var restorePlan = await driveBackupService.CreateRestorePlanAsync(bytes);
                if (restorePlan.HasColumnMismatch)
                {
                    ShowRestoreSchemaWarning(bytes, restorePlan);
                    return;
                }

                await RestoreBackupAsync(bytes, restorePlan);
            }
            catch (Exception ex)
            {
                SetRestoreBusyState(false);
                await DisplayAlert("Restore failed", ex.Message, "OK");
            }
        }

        private void ShowRestoreSchemaWarning(byte[] bytes, DriveBackupRestorePlan restorePlan)
        {
            pendingRestoreBytes = bytes;
            pendingRestorePlan = restorePlan;
            isRestoringBackup = false;
            RestoreSelectionContent.IsVisible = false;
            RestoreProgressContent.IsVisible = false;
            RestoreActivityIndicator.IsRunning = false;
            RestoreSchemaWarningContent.IsVisible = true;
            RestoreSchemaWarningDetailLabel.Text =
                "This backup was created with progress columns that do not exactly match the current database. " +
                "Only matching score, mistake, and learning columns will be restored.";
            RestoreSchemaWarningColumnsLabel.Text = BuildRestoreSchemaWarningText(restorePlan);
            SetRestoreSchemaWarningConfirmationState(false);
        }

        private void SetRestoreSchemaWarningConfirmationState(bool isConfirmed)
        {
            isRestoreSchemaWarningConfirmed = isConfirmed;
            RestoreSchemaWarningConfirmationBox.BackgroundColor = Color.FromArgb(
                isConfirmed ? "#2563EB" : "#FFFFFF");
            RestoreSchemaWarningConfirmationBox.Stroke = Color.FromArgb("#2563EB");
            RestoreSchemaWarningConfirmationCheckMark.IsVisible = isConfirmed;
            RestoreSchemaWarningConfirmButton.InputTransparent = !isConfirmed;
            RestoreSchemaWarningConfirmButton.Opacity = isConfirmed ? 1 : 0.6;
            RestoreSchemaWarningConfirmButton.BackgroundColor = Color.FromArgb(
                isConfirmed ? "#2563EB" : "#EAF1FF");
            RestoreSchemaWarningConfirmButton.Stroke = Color.FromArgb(
                isConfirmed ? "#2563EB" : "#CFE0FF");
            RestoreSchemaWarningConfirmLabel.TextColor = Color.FromArgb(
                isConfirmed ? "#FFFFFF" : "#2563EB");
        }

        private static string BuildRestoreSchemaWarningText(DriveBackupRestorePlan restorePlan)
        {
            var builder = new StringBuilder();
            AppendColumnList(builder, "Will restore", restorePlan.MatchingColumns);
            AppendColumnList(builder, "Missing from backup", restorePlan.MissingColumns);
            AppendColumnList(builder, "Ignored backup columns", restorePlan.UnexpectedColumns);
            return builder.ToString().TrimEnd();
        }

        private static void AppendColumnList(
            StringBuilder builder,
            string title,
            IReadOnlyList<string> columns)
        {
            builder.Append(title);
            builder.Append(": ");
            builder.AppendLine(columns.Count == 0 ? "none" : string.Join(", ", columns));
        }

        private async Task RestoreBackupAsync(byte[] bytes, DriveBackupRestorePlan restorePlan)
        {
            ShowRestoreProgressState(
                "Restoring backup...",
                selectedRestoreBackup?.Modified.LocalDateTime.ToString("f", CultureInfo.CurrentCulture) ??
                    "Applying backup data.");

            try
            {
                var progress = new Progress<int>(value =>
                {
                    RestoreProgressLabel.Text = value < 100 ? $"Restoring {value}%" : "Restoring backup...";
                });

                await driveBackupService.RestoreFromZipAsync(bytes, restorePlan.MatchingColumns, progress);
                SettingsTabContent.RefreshValues();
                await HomeTabContent.RefreshAsync();
                await PracticeTabContent.RefreshStatsAsync();
                RestoreProgressLabel.Text = "Restore complete";
                RestoreProgressDetailLabel.Text = "Your progress has been restored.";
                await Task.Delay(700);
                await HideRestoreOverlayAsync();
            }
            catch (Exception ex)
            {
                SetRestoreBusyState(false);
                await DisplayAlert("Restore failed", ex.Message, "OK");
            }
        }

        private async Task HideRestoreOverlayAsync()
        {
            if (!RestoreOverlay.IsVisible)
            {
                return;
            }

            await RestoreOverlay.FadeTo(0, 100, Easing.CubicIn);
            RestoreOverlay.IsVisible = false;
            RestoreOverlay.Opacity = 1;
            availableRestoreBackups = [];
            selectedRestoreBackup = null;
            pendingRestoreBytes = null;
            pendingRestorePlan = null;
            isRestoreConfirmed = false;
            isRestoreSchemaWarningConfirmed = false;
            RestoreBackupListContainer.Children.Clear();
            isRestoringBackup = false;
            RestoreSelectionContent.IsVisible = true;
            RestoreProgressContent.IsVisible = false;
            RestoreSchemaWarningContent.IsVisible = false;
            RestoreActivityIndicator.IsRunning = false;
            SetRestoreConfirmationState(false);
            SetRestoreSchemaWarningConfirmationState(false);
            UpdateRestoreConfirmButtonState();
        }
    }
}
