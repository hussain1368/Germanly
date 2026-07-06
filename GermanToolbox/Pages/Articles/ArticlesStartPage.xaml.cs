namespace GermanToolbox
{
    public partial class ArticlesStartPage : ContentPage
    {
        private readonly Color selectedCaseBackground = Color.FromArgb("#EAF1FF");
        private readonly Color selectedCaseStroke = Color.FromArgb("#2563EB");
        private readonly Color selectedCaseText = Color.FromArgb("#2563EB");
        private readonly Color unselectedBackground = Colors.White;
        private readonly Color unselectedStroke = Color.FromArgb("#E4E4DE");
        private readonly Color unselectedTitle = Color.FromArgb("#171717");
        private readonly Color unselectedSubtitle = Color.FromArgb("#777777");
        private readonly TestSessionService testSessionService;
        private readonly PracticeSettingsService settingsService;
        private string selectedLevel = string.Empty;
        private ArticleCase selectedCase = ArticleCase.Nominative;
        private ArticleType selectedArticleType = ArticleType.Definite;
        private int statsRefreshVersion;

        public ArticlesStartPage()
        {
            InitializeComponent();
            testSessionService = AppServices.GetRequiredService<TestSessionService>();
            settingsService = AppServices.GetRequiredService<PracticeSettingsService>();
            selectedLevel = settingsService.ArticleLevel;
            selectedCase = settingsService.SelectedArticleCase;
            selectedArticleType = settingsService.SelectedArticleType;
            SelectLevel(selectedLevel, persistSelection: false, refreshStats: false);
            SelectCase(selectedCase, persistSelection: false);
            SelectArticleType(selectedArticleType, persistSelection: false);
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

        private void OnNominativeTapped(object sender, TappedEventArgs e)
        {
            SelectCase(ArticleCase.Nominative);
        }

        private void OnAccusativeTapped(object sender, TappedEventArgs e)
        {
            SelectCase(ArticleCase.Accusative);
        }

        private void OnDativeTapped(object sender, TappedEventArgs e)
        {
            SelectCase(ArticleCase.Dative);
        }

        private void OnGenitiveTapped(object sender, TappedEventArgs e)
        {
            SelectCase(ArticleCase.Genitive);
        }

        private void OnDefiniteTapped(object sender, TappedEventArgs e)
        {
            SelectArticleType(ArticleType.Definite);
        }

        private void OnIndefiniteTapped(object sender, TappedEventArgs e)
        {
            SelectArticleType(ArticleType.Indefinite);
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

        private async void OnStartTestClicked(object sender, EventArgs e)
        {
            var session = await testSessionService.StartRegularSessionAsync(
                PracticeMode.Article,
                articleCase: selectedCase,
                articleType: selectedArticleType);
            if (session.TotalCount == 0)
            {
                await ToastService.ShowAsync(
                    this,
                    $"No {GetSelectedLevelPrefix()}nouns available for the article test.");
                return;
            }

            await Shell.Current.GoToAsync(nameof(ArticlesTestPage));
        }

        private async void OnReviewMistakesClicked(object sender, EventArgs e)
        {
            var session = await testSessionService.StartMistakeReviewSessionAsync(
                PracticeMode.Article,
                articleCase: selectedCase,
                articleType: selectedArticleType);
            if (session.TotalCount == 0)
            {
                await ToastService.ShowAsync(
                    this,
                    $"No {GetSelectedLevelPrefix()}article mistakes to review yet.");
                return;
            }

            await Shell.Current.GoToAsync(nameof(ArticlesTestPage));
        }

        private async void OnBottomTabSelected(object sender, TabSelectedEventArgs e)
        {
            await Shell.Current.GoToAsync($"//MainTabsPage?tab={e.Tab}");
        }

        private void SelectCase(ArticleCase articleCase, bool persistSelection = true)
        {
            selectedCase = articleCase;
            if (persistSelection)
            {
                settingsService.SelectedArticleCase = articleCase;
            }

            ResetCase(NominativeCase, NominativeCaseTitle, NominativeCaseSubtitle);
            ResetCase(AccusativeCase, AccusativeCaseTitle, AccusativeCaseSubtitle);
            ResetCase(DativeCase, DativeCaseTitle, DativeCaseSubtitle);
            ResetCase(GenitiveCase, GenitiveCaseTitle, GenitiveCaseSubtitle);

            var (selectedCard, selectedTitle, selectedSubtitle) = articleCase switch
            {
                ArticleCase.Accusative => (
                    AccusativeCase,
                    AccusativeCaseTitle,
                    AccusativeCaseSubtitle),
                ArticleCase.Dative => (
                    DativeCase,
                    DativeCaseTitle,
                    DativeCaseSubtitle),
                ArticleCase.Genitive => (
                    GenitiveCase,
                    GenitiveCaseTitle,
                    GenitiveCaseSubtitle),
                _ => (
                    NominativeCase,
                    NominativeCaseTitle,
                    NominativeCaseSubtitle)
            };

            selectedCard.BackgroundColor = selectedCaseBackground;
            selectedCard.Stroke = selectedCaseStroke;
            selectedCard.StrokeThickness = 2;
            selectedTitle.TextColor = selectedCaseText;
            selectedSubtitle.TextColor = selectedCaseText;

            UpdateOptionDescriptions();
        }

        private void ResetCase(Border card, Label title, Label subtitle)
        {
            card.BackgroundColor = unselectedBackground;
            card.Stroke = unselectedStroke;
            card.StrokeThickness = 1;
            title.TextColor = unselectedTitle;
            subtitle.TextColor = unselectedSubtitle;
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
                settingsService.ArticleLevel = selectedLevel;
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

            selectedCard.BackgroundColor = selectedCaseBackground;
            selectedCard.Stroke = selectedCaseStroke;
            selectedCard.StrokeThickness = 2;
            selectedTitle.TextColor = selectedCaseText;

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

        private void SelectArticleType(ArticleType articleType, bool persistSelection = true)
        {
            selectedArticleType = articleType;
            if (persistSelection)
            {
                settingsService.SelectedArticleType = articleType;
            }

            ResetArticleType(DefiniteType, DefiniteTypeTitle, DefiniteTypeSubtitle);
            ResetArticleType(IndefiniteType, IndefiniteTypeTitle, IndefiniteTypeSubtitle);

            var selectedCard = articleType == ArticleType.Definite ? DefiniteType : IndefiniteType;
            var selectedTitle = articleType == ArticleType.Definite
                ? DefiniteTypeTitle
                : IndefiniteTypeTitle;
            var selectedSubtitle = articleType == ArticleType.Definite
                ? DefiniteTypeSubtitle
                : IndefiniteTypeSubtitle;

            selectedCard.BackgroundColor = selectedCaseBackground;
            selectedCard.Stroke = selectedCaseStroke;
            selectedCard.StrokeThickness = 2;
            selectedTitle.TextColor = selectedCaseText;
            selectedSubtitle.TextColor = selectedCaseText;

            UpdateOptionDescriptions();
        }

        private void ResetArticleType(Border card, Label title, Label subtitle)
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
            var summaryTask = testSessionService.GetPracticeModeSummaryAsync(
                PracticeMode.Article,
                level: selectedLevelSnapshot);
            var levelSummariesTask = LevelProgressHelper.GetSummariesAsync(
                levelName => testSessionService.GetPracticeModeSummaryAsync(
                    PracticeMode.Article,
                    level: levelName));

            await Task.WhenAll(summaryTask, levelSummariesTask);

            if (refreshVersion != statsRefreshVersion)
            {
                return;
            }

            var summary = await summaryTask;
            var levelSummaries = await levelSummariesTask;
            ArticleLearnedCountLabel.Text = summary.LearnedCount.ToString();
            ArticleDueCountLabel.Text = summary.RemainingCount.ToString();
            ArticleMistakeCountLabel.Text = summary.MistakeCount.ToString();
            ApplyLevelProgress(levelSummaries);
            SetReviewMistakesEnabled(summary.MistakeCount > 0);
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

        private void SetReviewMistakesEnabled(bool isEnabled)
        {
            ReviewMistakesButton.IsEnabled = isEnabled;
            ReviewMistakesButton.IsVisible = isEnabled;
        }

        private void UpdateOptionDescriptions()
        {
            NominativeCaseSubtitle.Text = GermanArticleService
                .GetForms(ArticleCase.Nominative, selectedArticleType)
                .Summary;
            AccusativeCaseSubtitle.Text = GermanArticleService
                .GetForms(ArticleCase.Accusative, selectedArticleType)
                .Summary;
            DativeCaseSubtitle.Text = GermanArticleService
                .GetForms(ArticleCase.Dative, selectedArticleType)
                .Summary;
            GenitiveCaseSubtitle.Text = GermanArticleService
                .GetForms(ArticleCase.Genitive, selectedArticleType)
                .Summary;

            DefiniteTypeSubtitle.Text = GermanArticleService
                .GetForms(selectedCase, ArticleType.Definite)
                .Summary;
            IndefiniteTypeSubtitle.Text = GermanArticleService
                .GetForms(selectedCase, ArticleType.Indefinite)
                .Summary;
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
