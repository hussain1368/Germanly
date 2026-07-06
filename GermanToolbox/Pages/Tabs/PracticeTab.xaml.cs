namespace GermanToolbox
{
    public partial class PracticeTab : ContentView
    {
        private readonly TestSessionService testSessionService;
        private readonly PracticeSettingsService settingsService;

        public PracticeTab()
        {
            InitializeComponent();
            testSessionService = AppServices.GetRequiredService<TestSessionService>();
            settingsService = AppServices.GetRequiredService<PracticeSettingsService>();
        }

        public async Task RefreshStatsAsync()
        {
            var vocabularySummary = await testSessionService.GetPracticeModeSummaryAsync(PracticeMode.Meaning);
            var articleSummary = await testSessionService.GetPracticeModeSummaryAsync(PracticeMode.Article);
            var pluralSummary = await testSessionService.GetPracticeModeSummaryAsync(PracticeMode.Plural);
            var irregularSummary = await testSessionService.GetPracticeModeSummaryAsync(
                PracticeMode.IrregularVerb,
                irregularVerbForm: settingsService.SelectedIrregularVerbForm);

            ApplyProgress(VocabularyProgressLabel, VocabularyProgressBar, vocabularySummary);
            ApplyProgress(ArticlesProgressLabel, ArticlesProgressBar, articleSummary);
            ApplyProgress(PluralsProgressLabel, PluralsProgressBar, pluralSummary);
            ApplyProgress(IrregularProgressLabel, IrregularProgressBar, irregularSummary);
        }

        private async void OnVocabularyModuleTapped(object sender, TappedEventArgs e) =>
            await Shell.Current.GoToAsync(nameof(VocabularyStartPage));

        private async void OnArticlesModuleTapped(object sender, TappedEventArgs e) =>
            await Shell.Current.GoToAsync(nameof(ArticlesStartPage));

        private async void OnPluralsModuleTapped(object sender, TappedEventArgs e) =>
            await Shell.Current.GoToAsync(nameof(PluralsStartPage));

        private async void OnIrregularVerbsModuleTapped(object sender, TappedEventArgs e) =>
            await Shell.Current.GoToAsync(nameof(IrregularVerbsStartPage));

        private async void OnRulesTapped(object sender, TappedEventArgs e) =>
            await Shell.Current.GoToAsync(nameof(PracticeRulesPage));

        private static void ApplyProgress(
            Label label,
            ProgressBar progressBar,
            PracticeModeSummary summary)
        {
            label.Text = $"{summary.LearnedPercent}%";
            progressBar.Progress = summary.Progress;
        }
    }
}
