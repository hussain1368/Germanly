namespace GermanToolbox
{
    public partial class IrregularVerbsStartPage : ContentPage
    {
        private readonly Color selectedBackground = Color.FromArgb("#EAF6EA");
        private readonly Color selectedStroke = Color.FromArgb("#2E7D32");
        private readonly Color selectedText = Color.FromArgb("#2E7D32");
        private readonly Color unselectedBackground = Colors.White;
        private readonly Color unselectedStroke = Color.FromArgb("#E4E4DE");
        private readonly Color unselectedTitle = Color.FromArgb("#171717");
        private readonly Color unselectedSubtitle = Color.FromArgb("#777777");
        private readonly TestSessionService testSessionService;
        private readonly PracticeSettingsService settingsService;
        private string selectedLevel;
        private IrregularVerbForm selectedForm;
        private IrregularTestMethod selectedMethod;
        private int statsRefreshVersion;

        public IrregularVerbsStartPage()
        {
            InitializeComponent();
            testSessionService = AppServices.GetRequiredService<TestSessionService>();
            settingsService = AppServices.GetRequiredService<PracticeSettingsService>();
            selectedLevel = settingsService.IrregularVerbLevel;
            selectedForm = settingsService.SelectedIrregularVerbForm;
            selectedMethod = settingsService.SelectedIrregularTestMethod;
            SelectLevel(selectedLevel, persist: false, refreshStats: false);
            SelectForm(selectedForm, persist: false);
            SelectMethod(selectedMethod, persist: false);
            _ = RefreshStatsAsync();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await RefreshStatsAsync();
        }

        private async void OnBackTapped(object sender, TappedEventArgs e) =>
            await Shell.Current.GoToAsync("..");

        private void OnA1Tapped(object sender, TappedEventArgs e) => SelectLevel("A1");

        private void OnA2Tapped(object sender, TappedEventArgs e) => SelectLevel("A2");

        private void OnB1Tapped(object sender, TappedEventArgs e) => SelectLevel("B1");

        private void OnB2Tapped(object sender, TappedEventArgs e) => SelectLevel("B2");

        private void OnC1Tapped(object sender, TappedEventArgs e) => SelectLevel("C1");

        private void OnPrateritumTapped(object sender, TappedEventArgs e) =>
            SelectForm(IrregularVerbForm.Prateritum);

        private void OnPerfectTapped(object sender, TappedEventArgs e) =>
            SelectForm(IrregularVerbForm.Perfect);

        private void OnMultipleChoiceTapped(object sender, TappedEventArgs e) =>
            SelectMethod(IrregularTestMethod.MultipleChoice);

        private void OnTypingTapped(object sender, TappedEventArgs e) =>
            SelectMethod(IrregularTestMethod.Typing);

        private void OnSelfAssessmentTapped(object sender, TappedEventArgs e) =>
            SelectMethod(IrregularTestMethod.SelfAssessment);

        private async void OnStartTestClicked(object sender, EventArgs e)
        {
            var session = await testSessionService.StartRegularSessionAsync(
                PracticeMode.IrregularVerb,
                irregularVerbForm: selectedForm,
                irregularTestMethod: selectedMethod);
            if (session.TotalCount == 0)
            {
                await ToastService.ShowAsync(
                    this,
                    $"No {GetSelectedLevelPrefix()}irregular verbs available.");
                return;
            }

            await Shell.Current.GoToAsync(nameof(IrregularVerbsTestPage));
        }

        private async void OnReviewMistakesClicked(object sender, EventArgs e)
        {
            var session = await testSessionService.StartMistakeReviewSessionAsync(
                PracticeMode.IrregularVerb,
                irregularVerbForm: selectedForm,
                irregularTestMethod: selectedMethod);
            if (session.TotalCount == 0)
            {
                await ToastService.ShowAsync(
                    this,
                    $"No {GetSelectedLevelPrefix()}irregular verb mistakes to review yet.");
                return;
            }

            await Shell.Current.GoToAsync(nameof(IrregularVerbsTestPage));
        }

        private async void OnBottomTabSelected(object sender, TabSelectedEventArgs e) =>
            await Shell.Current.GoToAsync($"//MainTabsPage?tab={e.Tab}");

        private void SelectLevel(
            string level,
            bool persist = true,
            bool refreshStats = true)
        {
            var normalizedLevel = NormalizeLevel(level);
            selectedLevel = persist &&
                string.Equals(selectedLevel, normalizedLevel, StringComparison.Ordinal)
                    ? string.Empty
                    : normalizedLevel;

            if (persist)
            {
                settingsService.IrregularVerbLevel = selectedLevel;
            }

            ResetLevel(A1Level, A1LevelTitle);
            ResetLevel(A2Level, A2LevelTitle);
            ResetLevel(B1Level, B1LevelTitle);
            ResetLevel(B2Level, B2LevelTitle);
            ResetLevel(C1Level, C1LevelTitle);

            if (string.IsNullOrWhiteSpace(selectedLevel))
            {
                if (refreshStats)
                {
                    _ = RefreshStatsAsync();
                }

                return;
            }

            var selectedOption = selectedLevel switch
            {
                "A2" => (A2Level, A2LevelTitle),
                "B1" => (B1Level, B1LevelTitle),
                "B2" => (B2Level, B2LevelTitle),
                "C1" => (C1Level, C1LevelTitle),
                _ => (A1Level, A1LevelTitle)
            };

            selectedOption.Item1.BackgroundColor = selectedBackground;
            selectedOption.Item1.Stroke = selectedStroke;
            selectedOption.Item1.StrokeThickness = 2;
            selectedOption.Item2.TextColor = selectedText;

            if (refreshStats)
            {
                _ = RefreshStatsAsync();
            }
        }

        private void ResetLevel(Border card, Label title)
        {
            card.BackgroundColor = unselectedBackground;
            card.Stroke = unselectedStroke;
            card.StrokeThickness = 1;
            title.TextColor = unselectedTitle;
        }

        private void SelectForm(IrregularVerbForm form, bool persist = true)
        {
            selectedForm = form;
            if (persist)
            {
                settingsService.SelectedIrregularVerbForm = form;
                _ = RefreshStatsAsync();
            }

            ResetOption(PrateritumOption, PrateritumTitle, PrateritumSubtitle);
            ResetOption(PerfectOption, PerfectTitle, PerfectSubtitle);

            var option = form switch
            {
                IrregularVerbForm.Perfect =>
                    (PerfectOption, PerfectTitle, PerfectSubtitle),
                _ => (PrateritumOption, PrateritumTitle, PrateritumSubtitle)
            };
            SelectOption(option.Item1, option.Item2, option.Item3);
        }

        private void SelectMethod(IrregularTestMethod method, bool persist = true)
        {
            selectedMethod = method;
            if (persist)
            {
                settingsService.SelectedIrregularTestMethod = method;
            }

            ResetOption(MultipleChoiceOption, MultipleChoiceTitle, MultipleChoiceSubtitle);
            ResetOption(TypingOption, TypingTitle, TypingSubtitle);
            ResetOption(SelfAssessmentOption, SelfAssessmentTitle, SelfAssessmentSubtitle);

            var option = method switch
            {
                IrregularTestMethod.Typing => (TypingOption, TypingTitle, TypingSubtitle),
                IrregularTestMethod.SelfAssessment =>
                    (SelfAssessmentOption, SelfAssessmentTitle, SelfAssessmentSubtitle),
                _ => (MultipleChoiceOption, MultipleChoiceTitle, MultipleChoiceSubtitle)
            };
            SelectOption(option.Item1, option.Item2, option.Item3);
        }

        private void SelectOption(Border card, Label title, Label subtitle)
        {
            card.BackgroundColor = selectedBackground;
            card.Stroke = selectedStroke;
            card.StrokeThickness = 2;
            title.TextColor = selectedText;
            subtitle.TextColor = selectedText;
        }

        private void ResetOption(Border card, Label title, Label subtitle)
        {
            card.BackgroundColor = unselectedBackground;
            card.Stroke = unselectedStroke;
            card.StrokeThickness = 1;
            title.TextColor = unselectedTitle;
            subtitle.TextColor = unselectedSubtitle;
        }

        private async Task RefreshStatsAsync()
        {
            var refreshVersion = Interlocked.Increment(ref statsRefreshVersion);
            var selectedLevelSnapshot = selectedLevel;
            var selectedFormSnapshot = selectedForm;
            var summaryTask = testSessionService.GetPracticeModeSummaryAsync(
                PracticeMode.IrregularVerb,
                irregularVerbForm: selectedFormSnapshot,
                level: selectedLevelSnapshot);
            var levelSummariesTask = LevelProgressHelper.GetSummariesAsync(
                levelName => testSessionService.GetPracticeModeSummaryAsync(
                    PracticeMode.IrregularVerb,
                    irregularVerbForm: selectedFormSnapshot,
                    level: levelName));

            await Task.WhenAll(summaryTask, levelSummariesTask);

            if (refreshVersion != statsRefreshVersion)
            {
                return;
            }

            var summary = await summaryTask;
            var levelSummaries = await levelSummariesTask;
            LearnedCountLabel.Text = summary.LearnedCount.ToString();
            DueCountLabel.Text = summary.RemainingCount.ToString();
            MistakeCountLabel.Text = summary.MistakeCount.ToString();
            ApplyLevelProgress(levelSummaries);
            ReviewMistakesButton.IsEnabled = summary.MistakeCount > 0;
            ReviewMistakesButton.IsVisible = summary.MistakeCount > 0;
        }

        private void ApplyLevelProgress(
            IReadOnlyDictionary<string, PracticeModeSummary> levelSummaries)
        {
            LevelProgressHelper.Apply(A1LevelProgress, levelSummaries["A1"]);
            LevelProgressHelper.Apply(A2LevelProgress, levelSummaries["A2"]);
            LevelProgressHelper.Apply(B1LevelProgress, levelSummaries["B1"]);
            LevelProgressHelper.Apply(B2LevelProgress, levelSummaries["B2"]);
            LevelProgressHelper.Apply(C1LevelProgress, levelSummaries["C1"]);
        }

        private string GetSelectedLevelPrefix() =>
            string.IsNullOrWhiteSpace(selectedLevel) ? string.Empty : $"{selectedLevel} ";

        private static string NormalizeLevel(string level)
        {
            var normalizedLevel = level.Trim().ToUpperInvariant();
            return normalizedLevel switch
            {
                "A1" or "A2" or "B1" or "B2" or "C1" => normalizedLevel,
                _ => string.Empty
            };
        }
    }
}
