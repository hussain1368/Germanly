namespace GermanToolbox
{
    public partial class VocabularyTestPage : ContentPage
    {
        private readonly TestSessionService testSessionService;
        private readonly AnswerFeedbackService answerFeedbackService;
        private bool isAnimating;

        public VocabularyTestPage()
        {
            InitializeComponent();
            testSessionService = AppServices.GetRequiredService<TestSessionService>();
            answerFeedbackService = AppServices.GetRequiredService<AnswerFeedbackService>();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (!ApplyCurrentWord())
            {
                await DisplayAlert("Kein aktiver Test", "Start a vocabulary test first.", "OK");
                await Shell.Current.GoToAsync("..");
            }
        }

        private async void OnWordCardTapped(object sender, TappedEventArgs e)
        {
            if (isAnimating || TranslationReveal.Opacity >= 1)
            {
                return;
            }

            isAnimating = true;

            await Task.WhenAll(
                TranslationReveal.FadeTo(1, 150, Easing.CubicOut),
                TranslationReveal.TranslateTo(0, 0, 150, Easing.CubicOut));

            isAnimating = false;
        }

        private async void OnLikeClicked(object sender, EventArgs e)
        {
            await HandleMeaningAnswerAsync(isCorrect: true);
        }

        private async void OnDislikeClicked(object sender, EventArgs e)
        {
            await HandleMeaningAnswerAsync(isCorrect: false);
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
            testSessionService.AbandonCurrentSession(PracticeMode.Meaning);
            await HideQuitConfirmationAsync();
            await Shell.Current.GoToAsync("..");
        }

        private async Task HandleMeaningAnswerAsync(bool isCorrect)
        {
            if (isAnimating)
            {
                return;
            }

            TestAnswerResult result;
            try
            {
                result = testSessionService.RecordMeaningAnswer(isCorrect);
            }
            catch (InvalidOperationException)
            {
                await DisplayAlert("Kein aktiver Test", "Start a vocabulary test first.", "OK");
                await Shell.Current.GoToAsync("..");
                return;
            }

            await answerFeedbackService.PlayAnswerAsync(result.IsCorrect);

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
                WordContent.TranslateTo(0, -16, 120, Easing.CubicIn),
                TranslationReveal.FadeTo(0, 100, Easing.CubicIn),
                TranslationReveal.TranslateTo(0, 12, 100, Easing.CubicIn));

            ApplyCurrentWord();
            WordContent.TranslationY = 16;

            await Task.WhenAll(
                WordContent.FadeTo(1, 150, Easing.CubicOut),
                WordContent.TranslateTo(0, 0, 150, Easing.CubicOut),
                WordCard.ScaleTo(1, 150, Easing.CubicOut));

            isAnimating = false;
        }

        private async Task FinishSessionAsync()
        {
            isAnimating = true;

            await WordCard.ScaleTo(0.97, 90, Easing.CubicIn);
            await testSessionService.CompleteCurrentSessionAsync();
            await Shell.Current.GoToAsync(nameof(VocabularyResultsPage));

            WordCard.Scale = 1;
            isAnimating = false;
        }

        private bool ApplyCurrentWord()
        {
            var session = testSessionService.CurrentSession;
            if (session?.Mode != PracticeMode.Meaning || session.CurrentWord is null)
            {
                return false;
            }

            var word = session.CurrentWord.Word;
            var isEnglishToGerman = session.VocabularyDirection == VocabularyTestDirection.EnglishToGerman;

            WordLabel.Text = isEnglishToGerman ? word.Translation : word.Word;
            WordLabel.FontSize = GenericHelper.GetPromptFontSize(WordLabel.Text);
            TranslationLabel.Text = isEnglishToGerman ? word.Word : word.Translation;
            RevealHintLabel.Text = isEnglishToGerman
                ? "Tap the card to reveal the German word"
                : "Tap the card to reveal the translation";
            RevealTitleLabel.Text = isEnglishToGerman ? "Deutsches Wort" : "Übersetzung";
            PartOfSpeechLabel.Text = word.Type.ToUpperInvariant();
            LevelLabel.Text = word.Level;
            ProgressLabel.Text = $"{session.CurrentIndex + 1} / {session.TotalCount}";
            TranslationReveal.Opacity = 0;
            TranslationReveal.TranslationY = 12;
            WordContent.Opacity = 1;
            WordContent.TranslationY = 0;
            WordCard.Scale = 1;

            return true;
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
