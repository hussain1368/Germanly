namespace GermanToolbox
{
    public partial class VocabularyStartPage : ContentPage
    {
        private readonly Color selectedBackground = Color.FromArgb("#EAF1FF");
        private readonly Color selectedStroke = Color.FromArgb("#2563EB");
        private readonly Color selectedText = Color.FromArgb("#2563EB");
        private readonly Color unselectedBackground = Colors.White;
        private readonly Color unselectedStroke = Color.FromArgb("#E4E4DE");
        private readonly Color unselectedText = Color.FromArgb("#171717");
        private readonly Color unselectedSubtleText = Color.FromArgb("#777777");
        private readonly TestSessionService testSessionService;
        private readonly PracticeSettingsService settingsService;
        private string selectedLevel = string.Empty;
        private VocabularyTestDirection selectedDirection = VocabularyTestDirection.GermanToEnglish;
        private int statsRefreshVersion;

        public VocabularyStartPage()
        {
            InitializeComponent();
            testSessionService = AppServices.GetRequiredService<TestSessionService>();
            settingsService = AppServices.GetRequiredService<PracticeSettingsService>();
            selectedLevel = settingsService.VocabularyLevel;
            selectedDirection = settingsService.VocabularyDirection;
            SelectLevel(selectedLevel, persistSelection: false, refreshStats: false);
            SelectDirection(selectedDirection, persistSelection: false, refreshStats: false);
            _ = RefreshStatsAsync();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await RefreshStatsAsync();
        }

        private async void OnBackTapped(object sender, TappedEventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        private void OnA1Tapped(object sender, TappedEventArgs e)
        {
            SelectLevel("A1");
        }

        private void OnA2Tapped(object sender, TappedEventArgs e)
        {
            SelectLevel("A2");
        }

        private void OnB1Tapped(object sender, TappedEventArgs e)
        {
            SelectLevel("B1");
        }

        private void OnB2Tapped(object sender, TappedEventArgs e)
        {
            SelectLevel("B2");
        }

        private void OnC1Tapped(object sender, TappedEventArgs e)
        {
            SelectLevel("C1");
        }

        private void OnGermanToEnglishTapped(object sender, TappedEventArgs e)
        {
            SelectDirection(VocabularyTestDirection.GermanToEnglish);
        }

        private void OnEnglishToGermanTapped(object sender, TappedEventArgs e)
        {
            SelectDirection(VocabularyTestDirection.EnglishToGerman);
        }

        private async void OnStartTestClicked(object sender, EventArgs e)
        {
            var session = await testSessionService.StartRegularSessionAsync(PracticeMode.Meaning, selectedDirection);
            if (session.TotalCount == 0)
            {
                await ToastService.ShowAsync(
                    this,
                    $"No {GetSelectedLevelPrefix()}vocabulary words available for this test.");
                return;
            }

            await Shell.Current.GoToAsync(nameof(VocabularyTestPage));
        }

        private async void OnReviewWordsClicked(object sender, EventArgs e)
        {
            var session = await testSessionService.StartMistakeReviewSessionAsync(PracticeMode.Meaning, selectedDirection);
            if (session.TotalCount == 0)
            {
                await ToastService.ShowAsync(
                    this,
                    $"No {GetSelectedLevelPrefix()}vocabulary mistakes to review yet.");
                return;
            }

            await Shell.Current.GoToAsync(nameof(VocabularyTestPage));
        }

        private async void OnBottomTabSelected(object sender, TabSelectedEventArgs e)
        {
            await Shell.Current.GoToAsync($"//MainTabsPage?tab={e.Tab}");
        }

        private void SelectLevel(
            string level,
            bool persistSelection = true,
            bool refreshStats = true)
        {
            var normalizedLevel = NormalizeLevel(level);
            selectedLevel = persistSelection &&
                string.Equals(selectedLevel, normalizedLevel, StringComparison.Ordinal)
                    ? string.Empty
                    : normalizedLevel;

            if (persistSelection)
            {
                settingsService.VocabularyLevel = selectedLevel;
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

            var (selectedCard, selectedTitle) = selectedLevel switch
            {
                "A2" => (A2Level, A2LevelTitle),
                "B1" => (B1Level, B1LevelTitle),
                "B2" => (B2Level, B2LevelTitle),
                "C1" => (C1Level, C1LevelTitle),
                _ => (A1Level, A1LevelTitle)
            };

            selectedCard.BackgroundColor = selectedBackground;
            selectedCard.Stroke = selectedStroke;
            selectedCard.StrokeThickness = 2;
            selectedTitle.TextColor = selectedText;

            if (refreshStats)
            {
                _ = RefreshStatsAsync();
            }
        }

        private void SelectDirection(
            VocabularyTestDirection direction,
            bool persistSelection = true,
            bool refreshStats = true)
        {
            selectedDirection = direction;
            if (persistSelection)
            {
                settingsService.VocabularyDirection = direction;
            }

            ResetDirection(
                GermanToEnglishDirection,
                GermanToEnglishDirectionTitle,
                GermanToEnglishDirectionSubtitle);
            ResetDirection(
                EnglishToGermanDirection,
                EnglishToGermanDirectionTitle,
                EnglishToGermanDirectionSubtitle);

            var selectedCard = direction == VocabularyTestDirection.GermanToEnglish
                ? GermanToEnglishDirection
                : EnglishToGermanDirection;
            var selectedTitle = direction == VocabularyTestDirection.GermanToEnglish
                ? GermanToEnglishDirectionTitle
                : EnglishToGermanDirectionTitle;
            var selectedSubtitle = direction == VocabularyTestDirection.GermanToEnglish
                ? GermanToEnglishDirectionSubtitle
                : EnglishToGermanDirectionSubtitle;

            selectedCard.BackgroundColor = selectedBackground;
            selectedCard.Stroke = selectedStroke;
            selectedCard.StrokeThickness = 2;
            selectedTitle.TextColor = selectedText;
            selectedSubtitle.TextColor = selectedText;

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
            title.TextColor = unselectedText;
        }

        private void ResetDirection(Border card, Label title, Label subtitle)
        {
            card.BackgroundColor = unselectedBackground;
            card.Stroke = unselectedStroke;
            card.StrokeThickness = 1;
            title.TextColor = unselectedText;
            subtitle.TextColor = unselectedSubtleText;
        }

        private async Task RefreshStatsAsync()
        {
            var refreshVersion = Interlocked.Increment(ref statsRefreshVersion);
            var direction = selectedDirection;
            var selectedLevelSnapshot = selectedLevel;
            var summaryTask = testSessionService.GetPracticeModeSummaryAsync(
                PracticeMode.Meaning,
                direction,
                level: selectedLevelSnapshot);
            var levelSummariesTask = LevelProgressHelper.GetSummariesAsync(
                levelName => testSessionService.GetPracticeModeSummaryAsync(
                    PracticeMode.Meaning,
                    direction,
                    level: levelName));

            await Task.WhenAll(summaryTask, levelSummariesTask);

            if (refreshVersion != statsRefreshVersion)
            {
                return;
            }

            var summary = await summaryTask;
            var levelSummaries = await levelSummariesTask;
            VocabularyLearnedCountLabel.Text = summary.LearnedCount.ToString();
            VocabularyMasteredCountLabel.Text = summary.MasteredCount.ToString();
            VocabularyDueCountLabel.Text = summary.RemainingCount.ToString();
            ApplyLevelProgress(levelSummaries);
            SetReviewWordsEnabled(summary.MistakeCount > 0);
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

        private void SetReviewWordsEnabled(bool isEnabled)
        {
            ReviewWordsButton.IsEnabled = isEnabled;
            ReviewWordsButton.IsVisible = isEnabled;
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
