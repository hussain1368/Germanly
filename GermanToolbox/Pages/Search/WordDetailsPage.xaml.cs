namespace GermanToolbox
{
    public partial class WordDetailsPage : ContentPage, IQueryAttributable
    {
        private static readonly Color NegativeScoreBackground = Color.FromArgb("#FFF1F0");
        private static readonly Color NegativeScoreStroke = Color.FromArgb("#E6A8A4");
        private static readonly Color NegativeScoreText = Color.FromArgb("#D43D35");
        private static readonly Color NormalScoreBackground = Color.FromArgb("#F7F7F5");
        private static readonly Color NormalScoreStroke = Color.FromArgb("#E4E4DE");
        private static readonly Color NormalScoreText = Color.FromArgb("#171717");
        private static readonly Color LearnedScoreBackground = Color.FromArgb("#EAF6EA");
        private static readonly Color LearnedScoreStroke = Color.FromArgb("#A9D2AC");
        private static readonly Color LearnedScoreText = Color.FromArgb("#2E7D32");
        private readonly PracticeSettingsService settingsService;
        private readonly RegularVerbFormGenerator regularVerbFormGenerator;
        private readonly WordRepository repository;

        public WordDetailsPage()
        {
            InitializeComponent();
            repository = AppServices.GetRequiredService<WordRepository>();
            settingsService = AppServices.GetRequiredService<PracticeSettingsService>();
            regularVerbFormGenerator =
                AppServices.GetRequiredService<RegularVerbFormGenerator>();
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("wordId", out var value) &&
                int.TryParse(value?.ToString(), out var wordId))
            {
                _ = LoadWordAsync(wordId);
            }
            else
            {
                _ = ShowLoadErrorAsync();
            }

            query.Clear();
        }

        private async Task LoadWordAsync(int wordId)
        {
            try
            {
                var word = await repository.GetWordByIdAsync(wordId);
                if (word is null)
                {
                    await ShowLoadErrorAsync();
                    return;
                }

                ApplyWord(word);
                LoadingOverlay.IsVisible = false;
            }
            catch (Exception)
            {
                await ShowLoadErrorAsync();
            }
        }

        private void ApplyWord(WordEntry word)
        {
            var isNoun = string.Equals(word.Type, "noun", StringComparison.OrdinalIgnoreCase);
            var isVerb = WordTypeClassifier.IsVerb(word.Type);
            var isArticleApplicable = isNoun && HasArticleGender(word.Gender);
            var isPluralApplicable = isNoun && !string.IsNullOrWhiteSpace(word.Plural);
            var areIrregularScoresApplicable =
                isVerb &&
                word.IsStrong &&
                !string.IsNullOrWhiteSpace(word.Past) &&
                !string.IsNullOrWhiteSpace(word.Perfekt);
            var visualStyle = WordVisualStyleResolver.Resolve(word);

            WordTextSpan.Text = word.Word;
            ArticleTextSpan.Text = isArticleApplicable
                ? $" ({GetArticle(word.Gender)})"
                : string.Empty;
            TranslationLabel.Text = word.Translation;
            TypeBadgeLabel.Text = word.Type;
            LevelBadgeLabel.Text = word.Level;
            PluralHeroPanel.IsVisible = isPluralApplicable;
            PluralHeroValueLabel.Text = word.Plural ?? string.Empty;
            ApplyWordVisualStyle(
                visualStyle,
                hasTypeAccent: isVerb || isArticleApplicable);

            var learnedThreshold = settingsService.LearnedThreshold;
            ApplyLearningBadge(
                word,
                learnedThreshold,
                isArticleApplicable,
                isPluralApplicable,
                areIrregularScoresApplicable);

            VerbDetailsCard.IsVisible = isVerb;
            if (isVerb)
            {
                VerbStrengthLabel.Text = word.IsStrong ? "Stark" : "Schwach";
                VerbStrengthLabel.TextColor = visualStyle.AccentColor;
                VerbStrengthBadge.Stroke = visualStyle.StrokeColor;
                PrateritumValueLabel.TextColor = visualStyle.AccentColor;
                PerfectValueLabel.TextColor = visualStyle.AccentColor;
                GeneratedFormsBadge.BackgroundColor = Colors.White;
                GeneratedFormsBadge.Stroke = visualStyle.StrokeColor;
                GeneratedFormsBadge.StrokeThickness = 1;
                GeneratedFormsBadgeLabel.TextColor = visualStyle.AccentColor;
                ApplyVerbForms(word);
            }

            ArrangeApplicableScoreCards(
                isArticleApplicable,
                isPluralApplicable,
                areIrregularScoresApplicable);
            ApplyVerbFormScoreBadges(
                word,
                learnedThreshold,
                areIrregularScoresApplicable);
            ApplyScoreStyle(
                MeaningScoreCard,
                MeaningScoreLabel,
                MeaningScoreCheck,
                word.ScoreMeaning,
                learnedThreshold);
            ApplyScoreStyle(
                ReverseMeaningScoreCard,
                ReverseMeaningScoreLabel,
                ReverseMeaningScoreCheck,
                word.ScoreReverseMeaning,
                learnedThreshold);
            ApplyScoreStyle(
                ArticleScoreCard,
                ArticleScoreLabel,
                ArticleScoreCheck,
                word.ScoreArticle,
                learnedThreshold);
            ApplyScoreStyle(
                PluralScoreCard,
                PluralScoreLabel,
                PluralScoreCheck,
                word.ScorePlural,
                learnedThreshold);
            ApplyScoreStyle(
                PrateritumScoreCard,
                PrateritumScoreLabel,
                PrateritumScoreCheck,
                word.ScoreIrregularPrateritum,
                learnedThreshold);
            ApplyScoreStyle(
                PerfectScoreCard,
                PerfectScoreLabel,
                PerfectScoreCheck,
                word.ScoreIrregularPerfect,
                learnedThreshold);
        }

        private void ArrangeApplicableScoreCards(
            bool isArticleApplicable,
            bool isPluralApplicable,
            bool areIrregularScoresApplicable)
        {
            NounScoresRow.IsVisible = isArticleApplicable || isPluralApplicable;
            VerbScoresRow.IsVisible = areIrregularScoresApplicable;
            ArticleScoreCard.IsVisible = isArticleApplicable;
            PluralScoreCard.IsVisible = isPluralApplicable;

            if (isArticleApplicable && isPluralApplicable)
            {
                Grid.SetColumn(ArticleScoreCard, 0);
                Grid.SetColumnSpan(ArticleScoreCard, 1);
                Grid.SetColumn(PluralScoreCard, 1);
                Grid.SetColumnSpan(PluralScoreCard, 1);
            }
            else if (isArticleApplicable)
            {
                Grid.SetColumn(ArticleScoreCard, 0);
                Grid.SetColumnSpan(ArticleScoreCard, 2);
            }
            else if (isPluralApplicable)
            {
                Grid.SetColumn(PluralScoreCard, 0);
                Grid.SetColumnSpan(PluralScoreCard, 2);
            }
        }

        private void ApplyWordVisualStyle(
            WordVisualStyle visualStyle,
            bool hasTypeAccent)
        {
            WordTextSpan.TextColor = hasTypeAccent
                ? visualStyle.AccentColor
                : Color.FromArgb("#F2F5F7");
            ArticleTextSpan.TextColor = Color.FromArgb("#D6DEE4");
            TranslationLabel.TextColor = Color.FromArgb("#D6DEE4");
            TypeBadgeBorder.BackgroundColor = Colors.White;
            TypeBadgeBorder.Stroke = Color.FromArgb("#DDD4EB");
            TypeBadgeLabel.TextColor = Color.FromArgb("#555555");
            LevelBadgeBorder.BackgroundColor = Colors.White;
            LevelBadgeBorder.Stroke = Color.FromArgb("#DDD4EB");
            LevelBadgeLabel.TextColor = Color.FromArgb("#555555");
            PluralHeroValueLabel.TextColor = Color.FromArgb("#D97706");
        }

        private void ApplyLearningBadge(
            WordEntry word,
            int learnedThreshold,
            bool isArticleApplicable,
            bool isPluralApplicable,
            bool areIrregularScoresApplicable)
        {
            var isFullyMastered =
                word.ScoreMeaning >= learnedThreshold &&
                word.ScoreReverseMeaning >= learnedThreshold &&
                (!isArticleApplicable || word.ScoreArticle >= learnedThreshold) &&
                (!isPluralApplicable || word.ScorePlural >= learnedThreshold) &&
                (!areIrregularScoresApplicable ||
                    (word.ScoreIrregularPrateritum >= learnedThreshold &&
                     word.ScoreIrregularPerfect >= learnedThreshold));

            LearningBadge.IsVisible = word.Learning || isFullyMastered;
            LearningBadgeLabel.Text = isFullyMastered ? "Vollständig gemeistert" : "Lernen";
            LearningBadge.BackgroundColor = Color.FromArgb(
                isFullyMastered ? "#EAF6EA" : "#FFF4E8");
            LearningBadge.Stroke = Color.FromArgb(
                isFullyMastered ? "#A9D2AC" : "#E8BF91");
            LearningBadgeLabel.TextColor = Color.FromArgb(
                isFullyMastered ? "#2E7D32" : "#9A4D00");
        }

        private static void ApplyScoreStyle(
            Border card,
            Label scoreLabel,
            Label checkmark,
            int score,
            int learnedThreshold)
        {
            scoreLabel.Text = score.ToString();

            if (score < 0)
            {
                card.BackgroundColor = NegativeScoreBackground;
                card.Stroke = NegativeScoreStroke;
                scoreLabel.TextColor = NegativeScoreText;
                checkmark.IsVisible = false;
                return;
            }

            if (score >= learnedThreshold)
            {
                card.BackgroundColor = LearnedScoreBackground;
                card.Stroke = LearnedScoreStroke;
                scoreLabel.TextColor = LearnedScoreText;
                checkmark.TextColor = LearnedScoreText;
                checkmark.IsVisible = true;
                return;
            }

            card.BackgroundColor = NormalScoreBackground;
            card.Stroke = NormalScoreStroke;
            scoreLabel.TextColor = NormalScoreText;
            checkmark.IsVisible = false;
        }

        private void ApplyVerbFormScoreBadges(
            WordEntry word,
            int learnedThreshold,
            bool areIrregularScoresApplicable)
        {
            PrateritumFormScoreBadge.IsVisible = areIrregularScoresApplicable;
            PerfectFormScoreBadge.IsVisible = areIrregularScoresApplicable;

            if (!areIrregularScoresApplicable)
            {
                return;
            }

            ApplyScoreBadgeStyle(
                PrateritumFormScoreBadge,
                PrateritumFormScoreLabel,
                word.ScoreIrregularPrateritum,
                learnedThreshold);
            ApplyScoreBadgeStyle(
                PerfectFormScoreBadge,
                PerfectFormScoreLabel,
                word.ScoreIrregularPerfect,
                learnedThreshold);
        }

        private static void ApplyScoreBadgeStyle(
            Border badge,
            Label scoreLabel,
            int score,
            int learnedThreshold)
        {
            scoreLabel.Text = score.ToString();

            if (score < 0)
            {
                badge.BackgroundColor = NegativeScoreBackground;
                badge.Stroke = NegativeScoreStroke;
                scoreLabel.TextColor = NegativeScoreText;
                return;
            }

            if (score >= learnedThreshold)
            {
                badge.BackgroundColor = LearnedScoreBackground;
                badge.Stroke = LearnedScoreStroke;
                scoreLabel.TextColor = LearnedScoreText;
                return;
            }

            badge.BackgroundColor = NormalScoreBackground;
            badge.Stroke = NormalScoreStroke;
            scoreLabel.TextColor = NormalScoreText;
        }

        private void ApplyVerbForms(WordEntry word)
        {
            if (word.IsStrong)
            {
                PrateritumValueLabel.Text = GetAvailableValue(word.Past);
                PerfectValueLabel.Text = GetAvailableValue(word.Perfekt);
                GeneratedFormsBadge.IsVisible = false;
                return;
            }

            var forms = regularVerbFormGenerator.Generate(word);
            PrateritumValueLabel.Text = GetAvailableValue(forms.Prateritum);
            PerfectValueLabel.Text = GetAvailableValue(forms.Perfect);
            GeneratedFormsBadge.IsVisible = true;
        }

        private async Task ShowLoadErrorAsync()
        {
            LoadingOverlay.IsVisible = false;
            await DisplayAlert(
                "Word unavailable",
                "The selected word could not be loaded.",
                "OK");
            await Shell.Current.GoToAsync("..");
        }

        private async void OnBackTapped(object sender, TappedEventArgs e) =>
            await Shell.Current.GoToAsync("..");

        private async void OnBottomTabSelected(object sender, TabSelectedEventArgs e) =>
            await Shell.Current.GoToAsync($"//MainTabsPage?tab={e.Tab}");

        private static string GetArticle(string? gender) =>
            gender?.ToLowerInvariant() switch
            {
                "m" => "der",
                "f" => "die",
                "n" => "das",
                _ => "Unavailable"
            };

        private static bool HasArticleGender(string? gender) =>
            gender is "m" or "f" or "n";

        private static string GetAvailableValue(string? value) =>
            string.IsNullOrWhiteSpace(value) ? "Nicht verfügbar" : value;

    }
}
