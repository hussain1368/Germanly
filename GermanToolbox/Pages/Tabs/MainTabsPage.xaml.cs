namespace GermanToolbox
{
    public partial class MainTabsPage : ContentPage, IQueryAttributable
    {
        private readonly TestSessionService testSessionService;
        private bool isClearingProgress;
        private bool isClearProgressConfirmed;

        public MainTabsPage()
        {
            InitializeComponent();
            testSessionService = AppServices.GetRequiredService<TestSessionService>();
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
                await DisplayAlert("Zurücksetzen fehlgeschlagen", $"Progress was not reset. {ex.Message}", "OK");
            }
            finally
            {
                isClearingProgress = false;
            }
        }

        protected override bool OnBackButtonPressed()
        {
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
    }
}
