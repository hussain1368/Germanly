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
        private bool isRefreshingValues;

        public SettingsTab()
        {
            InitializeComponent();
            settingsService = AppServices.GetRequiredService<PracticeSettingsService>();
            RefreshValues();
        }

        public event EventHandler? ClearProgressRequested;

        public event EventHandler? ProgressCriteriaChanged;

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
    }
}
