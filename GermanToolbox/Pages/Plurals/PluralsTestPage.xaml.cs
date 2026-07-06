namespace GermanToolbox
{
    public partial class PluralsTestPage : ContentPage
    {
        private const double KeyboardHeightThreshold = 120;
        private static readonly TimeSpan KeyboardResizeDelay = TimeSpan.FromMilliseconds(500);
        private readonly TestSessionService testSessionService;
        private readonly AnswerFeedbackService answerFeedbackService;
        private readonly PluralDistractorGenerator distractorGenerator;
        private DateTime answerEntryFocusedAt;
        private double expandedPageHeight;
        private double pageLayoutWidth;
        private bool isAnimating;
        private bool isKeyboardVisible;
        private bool isSwitchingAnswerEntry;
        private bool isSynchronizingAnswerText;
        private bool isTypingMode;
        private int toastVersion;

        public PluralsTestPage()
        {
            InitializeComponent();
            isKeyboardVisible = UsesResizableSoftKeyboard;
            testSessionService = AppServices.GetRequiredService<TestSessionService>();
            answerFeedbackService = AppServices.GetRequiredService<AnswerFeedbackService>();
            distractorGenerator = AppServices.GetRequiredService<PluralDistractorGenerator>();
        }

        protected override async void OnAppearing()
        {
            AndroidSoftInputModeService.UseResizeMode();
            base.OnAppearing();
            if (UsesResizableSoftKeyboard && PageLayout.Height > 0)
            {
                pageLayoutWidth = PageLayout.Width;
                expandedPageHeight = PageLayout.Height;
            }

            if (!ApplyCurrentWord())
            {
                await DisplayAlert("Kein aktiver Test", "Start a plural test first.", "OK");
                await Shell.Current.GoToAsync("..");
            }
        }

        private async void OnMultipleChoiceClicked(object? sender, EventArgs e)
        {
            if (sender is not Button button || isAnimating)
            {
                return;
            }

            var session = testSessionService.CurrentSession;
            var currentWord = session?.CurrentWord?.Word;
            if (session?.Mode != PracticeMode.Plural || currentWord is null)
            {
                return;
            }

            var isCorrect = PluralAnswerService.IsCorrect(currentWord, button.Text);
            if (!isCorrect)
            {
                button.BackgroundColor = Color.FromArgb("#FCEBEA");
                button.BorderColor = Color.FromArgb("#D43D35");
                button.TextColor = Color.FromArgb("#D43D35");
                button.IsEnabled = false;
            }

            await HandleAnswerAsync(isCorrect, completeOnIncorrect: false);
        }

        private async void OnSubmitAnswerClicked(object? sender, EventArgs e)
        {
            var answer = GetAnswerText();
            if (isAnimating || string.IsNullOrWhiteSpace(answer))
            {
                return;
            }

            var session = testSessionService.CurrentSession;
            var currentWord = session?.CurrentWord?.Word;
            if (session?.Mode != PracticeMode.Plural || currentWord is null)
            {
                return;
            }

            var isCorrect = PluralAnswerService.IsCorrect(currentWord, answer);
            if (!isCorrect)
            {
                SelectAnswerText();
                _ = ShowToastAsync("Not quite. Try again.");
            }

            await HandleAnswerAsync(isCorrect, completeOnIncorrect: false);
        }

        private void OnGermanCharacterClicked(object sender, EventArgs e)
        {
            if (sender is Button { CommandParameter: string character })
            {
                var entry = GetActiveAnswerEntry();
                var cursor = entry.CursorPosition;
                var text = entry.Text ?? string.Empty;
                entry.Text = text.Insert(Math.Clamp(cursor, 0, text.Length), character);
                entry.CursorPosition = Math.Min(
                    cursor + character.Length,
                    entry.Text.Length);
                entry.Focus();
            }
        }

        private async void OnWordCardTapped(object sender, TappedEventArgs e)
        {
            var session = testSessionService.CurrentSession;
            if (session?.Mode != PracticeMode.Plural ||
                session.IrregularTestMethod != IrregularTestMethod.SelfAssessment ||
                CardAnswerReveal.Opacity >= 1 ||
                isAnimating)
            {
                return;
            }

            SelfAssessmentActions.InputTransparent = false;
            await Task.WhenAll(
                SelfAssessmentActions.FadeTo(1, 140, Easing.CubicOut),
                CardAnswerReveal.FadeTo(1, 140, Easing.CubicOut));
        }

        private async void OnSelfAssessmentWrongClicked(object sender, EventArgs e) =>
            await HandleAnswerAsync(isCorrect: false, completeOnIncorrect: true);

        private async void OnSelfAssessmentCorrectClicked(object sender, EventArgs e) =>
            await HandleAnswerAsync(isCorrect: true, completeOnIncorrect: true);

        private async Task HandleAnswerAsync(bool isCorrect, bool completeOnIncorrect)
        {
            TestAnswerResult result;
            try
            {
                result = testSessionService.RecordPluralAnswer(isCorrect, completeOnIncorrect);
            }
            catch (InvalidOperationException)
            {
                await DisplayAlert("Kein aktiver Test", "Start a plural test first.", "OK");
                await Shell.Current.GoToAsync("..");
                return;
            }

            await answerFeedbackService.PlayAnswerAsync(result.IsCorrect);

            if (!result.ShouldAdvance)
            {
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

        private bool ApplyCurrentWord()
        {
            var session = testSessionService.CurrentSession;
            if (session?.Mode != PracticeMode.Plural || session.CurrentWord is null)
            {
                return false;
            }

            var word = session.CurrentWord.Word;
            NounLabel.Text = word.Word;
            NounLabel.FontSize = GenericHelper.GetPromptFontSize(word.Word);
            TranslationLabel.Text = word.Translation;
            LevelLabel.Text = word.Level;
            ProgressLabel.Text = $"{session.CurrentIndex + 1} / {session.TotalCount}";

            MultipleChoicePanel.IsVisible =
                session.IrregularTestMethod == IrregularTestMethod.MultipleChoice;
            isTypingMode = session.IrregularTestMethod == IrregularTestMethod.Typing;
            SelfAssessmentPanel.IsVisible =
                session.IrregularTestMethod == IrregularTestMethod.SelfAssessment;

            BuildMultipleChoiceOptions(word);
            SetAnswerText(string.Empty);
            var displayAnswer = PluralAnswerService.GetDisplayAnswer(word);
            RevealedAnswerLabel.Text = displayAnswer;
            RevealedAnswerLabel.FontSize = GetRevealAnswerFontSize(displayAnswer);
            RevealHintLabel.IsVisible =
                session.IrregularTestMethod == IrregularTestMethod.SelfAssessment;
            CardAnswerReveal.IsVisible =
                session.IrregularTestMethod == IrregularTestMethod.SelfAssessment;
            CardAnswerReveal.Opacity = 0;
            CardAnswerReveal.TranslationY = 0;
            SelfAssessmentActions.Opacity = 0.35;
            SelfAssessmentActions.InputTransparent = true;
            WordContent.Opacity = 1;
            WordContent.TranslationY = 0;
            WordCard.Scale = 1;
            WordCard.TranslationX = 0;

            if (isTypingMode)
            {
                SetTypingPanelPlacement(
                    useFloatingHost: UsesResizableSoftKeyboard && isKeyboardVisible);
                Dispatcher.DispatchDelayed(
                    TimeSpan.FromMilliseconds(150),
                    () =>
                    {
                        SetTypingPanelPlacement(useFloatingHost: UsesResizableSoftKeyboard);
                        GetActiveAnswerEntry().Focus();
                    });
            }
            else
            {
                InlineTypingPanel.IsVisible = false;
                FloatingTypingPanel.IsVisible = false;
                UnfocusAnswerEntries();
            }

            return true;
        }

        protected override void OnDisappearing()
        {
            UnfocusAnswerEntries();
            AndroidSoftInputModeService.UseOverlayMode();
            base.OnDisappearing();
        }

        private void OnAnswerEntryFocused(object? sender, FocusEventArgs e)
        {
            if (sender is not Entry entry || isSwitchingAnswerEntry)
            {
                return;
            }

            if (!UsesResizableSoftKeyboard)
            {
                return;
            }

            answerEntryFocusedAt = DateTime.UtcNow;
            isKeyboardVisible = true;
            if (ReferenceEquals(entry, InlineAnswerEntry))
            {
                ShowFloatingEntryAndTransferFocus();
            }
            else
            {
                SetTypingPanelPlacement(useFloatingHost: true);
            }

            Dispatcher.DispatchDelayed(KeyboardResizeDelay, UpdateTypingPanelPlacement);
        }

        private void OnAnswerEntryUnfocused(object? sender, FocusEventArgs e)
        {
            if (isSwitchingAnswerEntry || !UsesResizableSoftKeyboard)
            {
                return;
            }

            Dispatcher.Dispatch(UpdateTypingPanelPlacement);
        }

        private void OnAnswerEntryTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (isSynchronizingAnswerText || sender is not Entry sourceEntry)
            {
                return;
            }

            var targetEntry = ReferenceEquals(sourceEntry, InlineAnswerEntry)
                ? FloatingAnswerEntry
                : InlineAnswerEntry;
            isSynchronizingAnswerText = true;
            try
            {
                targetEntry.Text = e.NewTextValue ?? string.Empty;
            }
            finally
            {
                isSynchronizingAnswerText = false;
            }
        }

        private void OnPageLayoutSizeChanged(object? sender, EventArgs e)
        {
            if (!UsesResizableSoftKeyboard || PageLayout.Height <= 0)
            {
                return;
            }

            if (Math.Abs(PageLayout.Width - pageLayoutWidth) > 1)
            {
                pageLayoutWidth = PageLayout.Width;
                expandedPageHeight = PageLayout.Height;
            }
            else
            {
                expandedPageHeight = Math.Max(expandedPageHeight, PageLayout.Height);
            }

            UpdateTypingPanelPlacement();
        }

        private void UpdateTypingPanelPlacement()
        {
            if (!isTypingMode ||
                !UsesResizableSoftKeyboard ||
                expandedPageHeight <= 0)
            {
                return;
            }

            var heightReduction = expandedPageHeight - PageLayout.Height;
            var keyboardHasResizedPage = heightReduction >= KeyboardHeightThreshold;
            var focusResizeWindowElapsed =
                DateTime.UtcNow - answerEntryFocusedAt >= KeyboardResizeDelay;

            if (keyboardHasResizedPage)
            {
                isKeyboardVisible = true;
                SetTypingPanelPlacement(useFloatingHost: true);
            }
            else if (!FloatingAnswerEntry.IsFocused || focusResizeWindowElapsed)
            {
                isKeyboardVisible = false;
                SetTypingPanelPlacement(useFloatingHost: false);
            }
        }

        private void SetTypingPanelPlacement(bool useFloatingHost)
        {
            if (!isTypingMode)
            {
                InlineTypingPanel.IsVisible = false;
                FloatingTypingPanel.IsVisible = false;
                return;
            }

            var showFloating = UsesResizableSoftKeyboard && useFloatingHost;
            isSwitchingAnswerEntry = true;
            try
            {
                if (showFloating)
                {
                    FloatingTypingPanel.IsVisible = true;
                    InlineTypingPanel.IsVisible = false;
                }
                else
                {
                    InlineTypingPanel.IsVisible = true;
                    FloatingAnswerEntry.Unfocus();
                    FloatingTypingPanel.IsVisible = false;
                }
            }
            finally
            {
                isSwitchingAnswerEntry = false;
            }
        }

        private void ShowFloatingEntryAndTransferFocus()
        {
            isSwitchingAnswerEntry = true;
            try
            {
                var cursorPosition = InlineAnswerEntry.CursorPosition;
                FloatingTypingPanel.IsVisible = true;
                FloatingAnswerEntry.CursorPosition = Math.Min(
                    cursorPosition,
                    FloatingAnswerEntry.Text?.Length ?? 0);
                InlineAnswerEntry.Unfocus();
                FloatingAnswerEntry.Focus();
                InlineTypingPanel.IsVisible = false;
            }
            finally
            {
                isSwitchingAnswerEntry = false;
            }
        }

        private static bool UsesResizableSoftKeyboard =>
            DeviceInfo.Platform == DevicePlatform.Android;

        private Entry GetActiveAnswerEntry() =>
            FloatingTypingPanel.IsVisible
                ? FloatingAnswerEntry
                : InlineAnswerEntry;

        private string GetAnswerText() =>
            GetActiveAnswerEntry().Text ?? string.Empty;

        private void SetAnswerText(string text)
        {
            isSynchronizingAnswerText = true;
            try
            {
                InlineAnswerEntry.Text = text;
                FloatingAnswerEntry.Text = text;
            }
            finally
            {
                isSynchronizingAnswerText = false;
            }
        }

        private void SelectAnswerText()
        {
            var entry = GetActiveAnswerEntry();
            entry.CursorPosition = 0;
            entry.SelectionLength = entry.Text?.Length ?? 0;
        }

        private void UnfocusAnswerEntries()
        {
            isSwitchingAnswerEntry = true;
            try
            {
                InlineAnswerEntry.Unfocus();
                FloatingAnswerEntry.Unfocus();
            }
            finally
            {
                isSwitchingAnswerEntry = false;
            }
        }

        private async Task ShowToastAsync(string message)
        {
            var version = Interlocked.Increment(ref toastVersion);
            ToastLabel.Text = message;
            ToastCard.Opacity = 0;
            ToastCard.TranslationY = 18;
            ToastOverlay.IsVisible = true;

            await Task.WhenAll(
                ToastCard.FadeTo(1, 120, Easing.CubicOut),
                ToastCard.TranslateTo(0, 0, 120, Easing.CubicOut));
            await Task.Delay(1200);

            if (version != toastVersion)
            {
                return;
            }

            await Task.WhenAll(
                ToastCard.FadeTo(0, 120, Easing.CubicIn),
                ToastCard.TranslateTo(0, 12, 120, Easing.CubicIn));
            if (version == toastVersion)
            {
                ToastOverlay.IsVisible = false;
            }
        }

        private void BuildMultipleChoiceOptions(WordEntry word)
        {
            MultipleChoiceOptions.Children.Clear();
            var options = distractorGenerator
                .Generate(word, 3)
                .Concat(PluralAnswerService.GetAcceptedAnswers(word))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(_ => Random.Shared.Next())
                .ToList();

            foreach (var option in options)
            {
                var button = new Button
                {
                    BackgroundColor = Colors.White,
                    BorderColor = Color.FromArgb("#E4DDF2"),
                    BorderWidth = 2,
                    CornerRadius = 8,
                    FontAttributes = FontAttributes.Bold,
                    FontFamily = "OpenSansSemibold",
                    FontSize = GetOptionFontSize(option),
                    HeightRequest = 54,
                    HorizontalOptions = LayoutOptions.Fill,
                    Text = option,
                    TextColor = Color.FromArgb("#171717")
                };
                button.Clicked += OnMultipleChoiceClicked;
                MultipleChoiceOptions.Children.Add(button);
            }
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
            await Shell.Current.GoToAsync(nameof(PluralsResultsPage));
            WordCard.Scale = 1;
            isAnimating = false;
        }

        private async void OnQuitTapped(object sender, TappedEventArgs e)
        {
            UnfocusAnswerEntries();
            await ShowQuitConfirmationAsync();
        }

        protected override bool OnBackButtonPressed()
        {
            if (QuitConfirmationOverlay.IsVisible)
            {
                _ = HideQuitConfirmationAsync();
            }
            else
            {
                _ = ShowQuitConfirmationAsync();
            }

            return true;
        }

        private async void OnCancelQuitTapped(object sender, TappedEventArgs e) =>
            await HideQuitConfirmationAsync();

        private async void OnConfirmQuitTapped(object sender, TappedEventArgs e)
        {
            testSessionService.AbandonCurrentSession(PracticeMode.Plural);
            await HideQuitConfirmationAsync();
            await Shell.Current.GoToAsync("..");
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

        private static double GetOptionFontSize(string text) =>
            text.Length switch
            {
                > 38 => 13,
                > 28 => 14,
                > 20 => 16,
                _ => 18
            };

        private static double GetRevealAnswerFontSize(string text) =>
            text.Length switch
            {
                > 42 => 15,
                > 32 => 17,
                > 24 => 19,
                > 18 => 21,
                _ => 24
            };
    }
}
