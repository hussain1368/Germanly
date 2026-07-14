namespace GermanToolbox
{
    public partial class ArticlesTestPage : ContentPage
    {
        private static readonly Color DefaultNounColor = Colors.White;
        private static readonly Color MasculineNounColor = Color.FromArgb("#2563EB");
        private static readonly Color FeminineNounColor = Color.FromArgb("#D43D35");
        private static readonly Color NeuterNounColor = Color.FromArgb("#2E7D32");
        private readonly TestSessionService testSessionService;
        private readonly AnswerFeedbackService answerFeedbackService;
        private readonly WordRepository wordRepository;
        private bool isAnimating;

        public ArticlesTestPage()
        {
            InitializeComponent();
            testSessionService = AppServices.GetRequiredService<TestSessionService>();
            answerFeedbackService = AppServices.GetRequiredService<AnswerFeedbackService>();
            wordRepository = AppServices.GetRequiredService<WordRepository>();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (!ApplyCurrentWord())
            {
                await DisplayAlert("No active test", "Start an article test first.", "OK");
                await Shell.Current.GoToAsync("..");
            }
        }

        private async void OnArticleClicked(object sender, EventArgs e)
        {
            if (sender is Button { CommandParameter: string gender })
            {
                await HandleArticleAnswerAsync(gender);
            }
        }

        private async void OnQuitTapped(object sender, TappedEventArgs e)
        {
            await ShowQuitConfirmationAsync();
        }

        protected override bool OnBackButtonPressed()
        {
            if (QuitConfirmationOverlay.IsVisible)
            {
                _ = HideQuitConfirmationAsync();
                return true;
            }

            _ = ShowQuitConfirmationAsync();
            return true;
        }

        private async void OnCancelQuitTapped(object sender, TappedEventArgs e)
        {
            await HideQuitConfirmationAsync();
        }

        private async void OnConfirmQuitTapped(object sender, TappedEventArgs e)
        {
            testSessionService.AbandonCurrentSession(PracticeMode.Article);
            await HideQuitConfirmationAsync();
            await Shell.Current.GoToAsync("..");
        }

        private async Task HandleArticleAnswerAsync(string gender)
        {
            if (isAnimating)
            {
                return;
            }

            TestAnswerResult result;
            try
            {
                result = testSessionService.RecordArticleAnswer(gender);
            }
            catch (InvalidOperationException)
            {
                await DisplayAlert("No active test", "Start an article test first.", "OK");
                await Shell.Current.GoToAsync("..");
                return;
            }

            await answerFeedbackService.PlayAnswerAsync(result.IsCorrect);

            if (!result.IsCorrect)
            {
                await RevealCorrectGenderStyleAsync();
                await ShakeWordCardAsync();
                return;
            }

            if (result.IsSessionFinished)
            {
                await FinishSessionAsync();
                return;
            }

            await AnimateToCurrentWordAsync();
        }

        private async Task AnimateToCurrentWordAsync()
        {
            isAnimating = true;

            await WordCard.ScaleTo(0.97, 90, Easing.CubicIn);
            await Task.WhenAll(
                WordContent.FadeTo(0, 120, Easing.CubicIn),
                WordContent.TranslateTo(0, -16, 120, Easing.CubicIn));

            ApplyCurrentWord();
            WordContent.TranslationY = 16;

            await Task.WhenAll(
                WordContent.FadeTo(1, 150, Easing.CubicOut),
                WordContent.TranslateTo(0, 0, 150, Easing.CubicOut),
                WordCard.ScaleTo(1, 150, Easing.CubicOut));

            isAnimating = false;
        }

        private async Task ShakeWordCardAsync()
        {
            isAnimating = true;

            await WordCard.TranslateTo(-14, 0, 45, Easing.SinInOut);
            await WordCard.TranslateTo(14, 0, 70, Easing.SinInOut);
            await WordCard.TranslateTo(-10, 0, 55, Easing.SinInOut);
            await WordCard.TranslateTo(8, 0, 45, Easing.SinInOut);
            await WordCard.TranslateTo(0, 0, 45, Easing.SinInOut);

            isAnimating = false;
        }

        private async Task FinishSessionAsync()
        {
            isAnimating = true;

            await WordCard.ScaleTo(0.97, 90, Easing.CubicIn);
            await testSessionService.CompleteCurrentSessionAsync();
            await Shell.Current.GoToAsync(nameof(ArticlesResultsPage));

            WordCard.Scale = 1;
            isAnimating = false;
        }

        private bool ApplyCurrentWord()
        {
            var session = testSessionService.CurrentSession;
            if (session?.Mode != PracticeMode.Article || session.CurrentWord is null)
            {
                return false;
            }

            var word = session.CurrentWord.Word;
            var articleForms = ArticleService.GetForms(session.ArticleCase, session.ArticleType);

            NounLabel.Text = word.Word;
            NounLabel.FontSize = GenericHelper.GetPromptFontSize(NounLabel.Text);
            ResetNounStyle();
            HideArticleRuleReveal();
            MeaningLabel.Text = word.Translation;
            MeaningLabel.FontSize = GetMeaningFontSize(MeaningLabel.Text);
            LevelLabel.Text = word.Level;
            ArticleCaseLabel.Text = GermanUiText.FormatArticleCase(session.ArticleCase);
            MasculineArticleButton.Text = articleForms.Masculine;
            FeminineArticleButton.Text = articleForms.Feminine;
            NeuterArticleButton.Text = articleForms.Neuter;
            MasculineArticleButton.FontSize = GetArticleButtonFontSize(articleForms.Masculine);
            FeminineArticleButton.FontSize = GetArticleButtonFontSize(articleForms.Feminine);
            NeuterArticleButton.FontSize = GetArticleButtonFontSize(articleForms.Neuter);
            SemanticProperties.SetDescription(
                MasculineArticleButton,
                $"Article option {articleForms.Masculine}");
            SemanticProperties.SetDescription(
                FeminineArticleButton,
                $"Article option {articleForms.Feminine}");
            SemanticProperties.SetDescription(
                NeuterArticleButton,
                $"Article option {articleForms.Neuter}");
            ProgressLabel.Text = $"{session.CurrentIndex + 1} / {session.TotalCount}";
            WordContent.Opacity = 1;
            WordContent.TranslationY = 0;
            WordCard.Scale = 1;
            WordCard.TranslationX = 0;

            return true;
        }

        private async Task RevealCorrectGenderStyleAsync()
        {
            var word = testSessionService.CurrentSession?.CurrentWord?.Word;
            NounLabel.TextColor = GetGenderColor(word?.Gender);
            SetNounOutlineVisible(true);

            HideArticleRuleReveal();

            if (word?.GenderHint is not int hintId || hintId <= 0)
            {
                return;
            }

            var hint = await wordRepository.GetHintByIdAsync(hintId);
            if (hint is null || string.IsNullOrWhiteSpace(hint.Rule))
            {
                return;
            }

            RevealedRuleLabel.Text = hint.Rule.Trim();
            RuleApplicationPerLabel.Text = $"{hint.Percentage}%";
            CardArticleRuleReveal.IsVisible = true;
        }

        private void HideArticleRuleReveal()
        {
            CardArticleRuleReveal.IsVisible = false;
            RevealedRuleLabel.Text = string.Empty;
            RuleApplicationPerLabel.Text = string.Empty;
        }

        private void ResetNounStyle()
        {
            NounLabel.TextColor = DefaultNounColor;
            SetNounOutlineVisible(false);
        }

        private void SetNounOutlineVisible(bool isVisible)
        {
            NounOutlineLeftLabel.Text = NounLabel.Text;
            NounOutlineRightLabel.Text = NounLabel.Text;
            NounOutlineTopLabel.Text = NounLabel.Text;
            NounOutlineBottomLabel.Text = NounLabel.Text;

            NounOutlineLeftLabel.FontSize = NounLabel.FontSize;
            NounOutlineRightLabel.FontSize = NounLabel.FontSize;
            NounOutlineTopLabel.FontSize = NounLabel.FontSize;
            NounOutlineBottomLabel.FontSize = NounLabel.FontSize;

            NounOutlineLeftLabel.IsVisible = isVisible;
            NounOutlineRightLabel.IsVisible = isVisible;
            NounOutlineTopLabel.IsVisible = isVisible;
            NounOutlineBottomLabel.IsVisible = isVisible;
        }

        private static Color GetGenderColor(string? gender) =>
            gender switch
            {
                "m" => MasculineNounColor,
                "f" => FeminineNounColor,
                "n" => NeuterNounColor,
                _ => DefaultNounColor
            };

        private static double GetArticleButtonFontSize(string article) =>
            article.Length >= 5 ? 25 : 30;

        private static double GetMeaningFontSize(string text)
        {
            if (text.Length > 58)
            {
                return 13;
            }

            if (text.Length > 42)
            {
                return 15;
            }

            return 18;
        }

        private async Task ShowQuitConfirmationAsync()
        {
            if (isAnimating || QuitConfirmationOverlay.IsVisible)
            {
                return;
            }

            QuitConfirmationOverlay.Opacity = 0;
            QuitConfirmationOverlay.IsVisible = true;
            await QuitConfirmationOverlay.FadeTo(1, 120, Easing.CubicOut);
        }

        private async Task HideQuitConfirmationAsync()
        {
            if (!QuitConfirmationOverlay.IsVisible)
            {
                return;
            }

            await QuitConfirmationOverlay.FadeTo(0, 100, Easing.CubicIn);
            QuitConfirmationOverlay.IsVisible = false;
            QuitConfirmationOverlay.Opacity = 1;
        }
    }
}
