namespace GermanToolbox
{
    public partial class HomeTab : ContentView
    {
        private readonly WordRepository repository;
        private readonly TestSessionService testSessionService;
        private readonly PracticeSettingsService settingsService;
        private readonly GoogleAuthService googleAuthService;
        private const string WelcomeImageDatePreferenceKey = "HomeWelcomeImageDate";
        private const string WelcomeImageSourcePreferenceKey = "HomeWelcomeImageSource";
        private const string GuestUserName = "Guest";
        private static readonly string[] WelcomeImageSources = ["boy.png", "girl.png"];
        private readonly SemaphoreSlim refreshLock = new(1, 1);
        private PracticeMode focusMode = PracticeMode.Meaning;
        private VocabularyTestDirection focusVocabularyDirection =
            VocabularyTestDirection.GermanToEnglish;
        private bool isVocabularyCompletionFocus;
        private PracticeMode reviewMode = PracticeMode.Meaning;
        private WordEntry? wordOfTheDay;
        private IReadOnlyList<LevelMasterySummary> levelMasterySummaries = [];
        private bool isGoogleSignInBusy;

        public HomeTab()
        {
            InitializeComponent();
            repository = AppServices.GetRequiredService<WordRepository>();
            testSessionService = AppServices.GetRequiredService<TestSessionService>();
            settingsService = AppServices.GetRequiredService<PracticeSettingsService>();
            googleAuthService = AppServices.GetRequiredService<GoogleAuthService>();
            googleAuthService.AuthenticationStateChanged += OnAuthenticationStateChanged;
            ApplyDailyWelcomeImage();
            ApplySignedInUser();
        }

        public async Task RefreshAsync()
        {
            GreetingLabel.Text = GetGreeting(DateTime.Now.Hour);
            ApplyDailyWelcomeImage();
            ApplySignedInUser();
            await refreshLock.WaitAsync();
            try
            {
                var vocabularySummary =
                    await testSessionService.GetPracticeModeSummaryAsync(PracticeMode.Meaning);
                var articleSummary =
                    await testSessionService.GetPracticeModeSummaryAsync(PracticeMode.Article);
                var pluralSummary =
                    await testSessionService.GetPracticeModeSummaryAsync(PracticeMode.Plural);
                var irregularSummary =
                    await testSessionService.GetPracticeModeSummaryAsync(
                        PracticeMode.IrregularVerb,
                        irregularVerbForm: settingsService.SelectedIrregularVerbForm);
                var overallMastery =
                    await repository.GetOverallMasteryAsync(settingsService.LearnedThreshold);
                var levelMastery =
                    await repository.GetLevelMasterySummariesAsync(settingsService.LearnedThreshold);
                var dailyWord =
                    await repository.GetWordOfTheDayAsync(DateOnly.FromDateTime(DateTime.Today));

                var summaries = new Dictionary<PracticeMode, PracticeModeSummary>
                {
                    [PracticeMode.Meaning] = vocabularySummary,
                    [PracticeMode.Article] = articleSummary,
                    [PracticeMode.Plural] = pluralSummary,
                    [PracticeMode.IrregularVerb] = irregularSummary
                };
                var isFreshStart = IsFreshStart(
                    summaries,
                    overallMastery.MasteredCount);

                ApplyPracticeSummaries(summaries, isFreshStart);
                ApplyOverallMastery(
                    overallMastery.MasteredCount,
                    overallMastery.TotalCount,
                    isFreshStart);
                ApplyLevelMasterySummaries(levelMastery);
                ApplyWordOfTheDay(dailyWord);
            }
            finally
            {
                refreshLock.Release();
            }
        }

        private void ApplySignedInUser()
        {
            var currentUser = googleAuthService.CurrentUser;
            UserNameLabel.Text = currentUser?.FirstName ?? GuestUserName;
            UserAvatarImage.Source =
                string.IsNullOrWhiteSpace(currentUser?.PhotoPath) ||
                !File.Exists(currentUser.PhotoPath)
                    ? "profile_avatar.png"
                    : ImageSource.FromFile(currentUser.PhotoPath);
        }

        private void OnAuthenticationStateChanged(object? sender, EventArgs e) =>
            ApplySignedInUser();

        private void ApplyDailyWelcomeImage()
        {
            var todayKey = DateTime.Today.ToString(
                "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture);
            var storedDate = Preferences.Default.Get(
                WelcomeImageDatePreferenceKey,
                string.Empty);
            var storedSource = Preferences.Default.Get(
                WelcomeImageSourcePreferenceKey,
                string.Empty);

            if (storedDate != todayKey ||
                !WelcomeImageSources.Contains(storedSource))
            {
                storedSource =
                    WelcomeImageSources[Random.Shared.Next(WelcomeImageSources.Length)];
                Preferences.Default.Set(WelcomeImageDatePreferenceKey, todayKey);
                Preferences.Default.Set(WelcomeImageSourcePreferenceKey, storedSource);
            }

            WelcomePersonImage.Source = storedSource;
        }

        private void ApplyPracticeSummaries(
            IReadOnlyDictionary<PracticeMode, PracticeModeSummary> summaries,
            bool isFreshStart)
        {
            var totalMistakes = summaries.Values.Sum(summary => summary.MistakeCount);
            var hasStartedPractice = summaries.Values.Any(summary =>
                summary.LearningCount > 0 || summary.MistakeCount > 0);

            FocusTitleLabel.Text = isFreshStart
                ? hasStartedPractice
                    ? "Weiter mit einer kurzen Deutschrunde?"
                    : "Bereit f\u00fcr deine erste Deutschrunde?"
                : "Bereit f\u00fcr eine kurze Deutschrunde?";
            OverviewSubtitleLabel.Text = isFreshStart
                ? "W\u00e4hle deinen ersten Einstieg in die Toolbox"
                : "Your learning progress at a glance";

            if (isFreshStart)
            {
                ApplyFreshStartModeSummaries();
                ApplyFreshStartFocus(totalMistakes, hasStartedPractice);
            }
            else
            {
                ApplyModeSummary(
                    VocabularyPercentLabel,
                    VocabularyLearnedLabel,
                    summaries[PracticeMode.Meaning]);
                ApplyModeSummary(
                    ArticlesPercentLabel,
                    ArticlesLearnedLabel,
                    summaries[PracticeMode.Article]);
                ApplyModeSummary(
                    PluralsPercentLabel,
                    PluralsLearnedLabel,
                    summaries[PracticeMode.Plural]);
                ApplyModeSummary(
                    IrregularPercentLabel,
                    IrregularLearnedLabel,
                    summaries[PracticeMode.IrregularVerb]);

                ApplyFocusSummary(summaries);
            }

            var review = summaries
                .OrderByDescending(pair => pair.Value.MistakeCount)
                .First();
            reviewMode = review.Key;
            ReviewCard.IsVisible = totalMistakes > 0;
            ReviewTitleLabel.Text =
                totalMistakes == 1
                    ? "1 Fehler bereit zur Wiederholung"
                    : $"{totalMistakes} Fehler bereit zur Wiederholung";
            ReviewSubtitleLabel.Text =
                $"Start with {GetEnglishModeName(reviewMode).ToLowerInvariant()}, where the most are waiting.";
        }

        private void ApplyFocusSummary(
            IReadOnlyDictionary<PracticeMode, PracticeModeSummary> summaries)
        {
            var focus = SelectFocusMode(summaries, settingsService.TestChunkSize);
            focusMode = focus.Mode;
            var isVocabularyFocus = focusMode == PracticeMode.Meaning;
            var vocabularySummary = summaries[PracticeMode.Meaning];
            isVocabularyCompletionFocus =
                isVocabularyFocus && vocabularySummary.PartiallyMasteredCount > 0;
            if (isVocabularyCompletionFocus)
            {
                focusVocabularyDirection = SelectVocabularyCompletionDirection(
                    vocabularySummary,
                    settingsService.VocabularyDirection);
            }

            FocusDescriptionLabel.Text = isVocabularyCompletionFocus
                ? GetVocabularyCompletionDescription(
                    focusVocabularyDirection,
                    GetVocabularyCompletionCount(
                        vocabularySummary,
                        focusVocabularyDirection))
                : isVocabularyFocus
                ? "Every strong German instinct starts here. Let's master a few more words in both directions."
                : GetAdvancedFocusDescription(
                    focusMode,
                    vocabularySummary.MasteredCount);
            FocusModeLabel.Text = GetGermanModeName(focusMode);
            var focusDueCount = isVocabularyCompletionFocus
                ? GetVocabularyCompletionCount(
                    vocabularySummary,
                    focusVocabularyDirection)
                : isVocabularyFocus
                ? focus.Summary.RemainingCount
                : focus.Summary.VocabularyMasteredRemainingCount;
            FocusDueLabel.Text = $"{focusDueCount} f\u00e4llig";
            FocusMistakeLabel.Text = FormatMistakeCount(focus.Summary.MistakeCount);
        }

        private void ApplyFreshStartFocus(int totalMistakes, bool hasStartedPractice)
        {
            focusMode = PracticeMode.Meaning;
            isVocabularyCompletionFocus = false;
            focusVocabularyDirection = settingsService.VocabularyDirection;
            FocusDescriptionLabel.Text = hasStartedPractice
                ? "Stay with vocabulary for now. The progress cards will switch automatically once a first value appears."
                : "Start with vocabulary. Articles, plurals, and strong verbs make more sense once a few words are in place.";
            FocusModeLabel.Text = GetGermanModeName(focusMode);
            FocusDueLabel.Text = hasStartedPractice ? "Weiter \u00fcben" : "Erste Runde";
            FocusMistakeLabel.Text =
                totalMistakes == 0 ? "Keine Fehler" : FormatMistakeCount(totalMistakes);
        }

        private void ApplyFreshStartModeSummaries()
        {
            VocabularyPercentLabel.Text = "Start";
            VocabularyLearnedLabel.Text = "Recommended start";
            ArticlesPercentLabel.Text = "Later";
            ArticlesLearnedLabel.Text = "After the first words";
            PluralsPercentLabel.Text = "Later";
            PluralsLearnedLabel.Text = "After the first words";
            IrregularPercentLabel.Text = "Build";
            IrregularLearnedLabel.Text = "Advanced round";
        }

        private void ApplyOverallMastery(
            int masteredCount,
            int totalCount,
            bool isFreshStart)
        {
            if (isFreshStart)
            {
                MasteryIconBackground.BackgroundColor = Color.FromArgb("#EAF1FF");
                MasteryIconPath.Stroke = Color.FromArgb("#2563EB");
                MasteryTitleLabel.Text = "Dein Startpunkt";
                MasteryDescriptionLabel.Text =
                    "A word is mastered after it reaches the target in every quiz that applies to it.";
                MasteredCountLabel.FontAttributes = FontAttributes.None;
                MasteredCountLabel.FontFamily = "OpenSansRegular";
                MasteredCountLabel.FontSize = 22;
                MasteredCountLabel.Text = "Not yet";
                MasteredCountLabel.TextColor = Color.FromArgb("#2563EB");
                MasteredTotalLabel.Text = "No mastered words";
                OverallMasteryCaptionLabel.Text = "Progress appears after your first mastered word";
                OverallMasteryPercentLabel.Text = "Pending";
                OverallMasteryPercentLabel.TextColor = Color.FromArgb("#2563EB");
                OverallMasteryProgressBar.BackgroundColor = Color.FromArgb("#EAF1FF");
                OverallMasteryProgressBar.ProgressColor = Color.FromArgb("#2563EB");
                OverallMasteryProgressBar.Progress = 0;
                return;
            }

            var progress = totalCount == 0 ? 0 : (double)masteredCount / totalCount;
            MasteryIconBackground.BackgroundColor = Color.FromArgb("#EAF6EA");
            MasteryIconPath.Stroke = Color.FromArgb("#2E7D32");
            MasteryTitleLabel.Text = "Gemeisterte W\u00f6rter";
            MasteryDescriptionLabel.Text =
                "Target reached in every applicable quiz";
            MasteredCountLabel.FontAttributes = FontAttributes.Bold;
            MasteredCountLabel.FontFamily = "OpenSansSemibold";
            MasteredCountLabel.FontSize = 34;
            MasteredCountLabel.Text = masteredCount.ToString();
            MasteredCountLabel.TextColor = Color.FromArgb("#2E7D32");
            MasteredTotalLabel.Text = $"of {totalCount} words";
            OverallMasteryCaptionLabel.Text = "Overall progress";
            OverallMasteryPercentLabel.Text =
                $"{(int)Math.Round(progress * 100)}%";
            OverallMasteryPercentLabel.TextColor = Color.FromArgb("#2E7D32");
            OverallMasteryProgressBar.BackgroundColor = Color.FromArgb("#EAF6EA");
            OverallMasteryProgressBar.ProgressColor = Color.FromArgb("#2E7D32");
            OverallMasteryProgressBar.Progress = progress;
        }

        private void ApplyLevelMasterySummaries(
            IReadOnlyList<LevelMasterySummary> summaries)
        {
            levelMasterySummaries = summaries;
        }

        private static void ApplyLevelMasteryRows(
            VerticalStackLayout target,
            IReadOnlyList<LevelMasterySummary> summaries,
            bool includeEmptyMessage = false)
        {
            target.Children.Clear();

            if (summaries.Count == 0)
            {
                if (includeEmptyMessage)
                {
                    target.Children.Add(new Label
                    {
                        FontSize = 12,
                        LineBreakMode = LineBreakMode.WordWrap,
                        Text = "No levels in progress yet.",
                        TextColor = Color.FromArgb("#777777")
                    });
                }

                return;
            }

            for (var index = 0; index < summaries.Count; index++)
            {
                target.Children.Add(CreateLevelMasteryRow(summaries[index]));

                if (index < summaries.Count - 1)
                {
                    target.Children.Add(new BoxView
                    {
                        BackgroundColor = Color.FromArgb("#EEF2F7"),
                        HeightRequest = 1
                    });
                }
            }
        }

        private static View CreateLevelMasteryRow(LevelMasterySummary summary)
        {
            var progress = summary.ActiveCount == 0
                ? 0
                : (double)summary.MasteredCount / summary.ActiveCount;
            var percent = (int)Math.Round(progress * 100);
            var row = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 12
            };

            var levelBadge = new Border
            {
                BackgroundColor = Color.FromArgb("#EAF1FF"),
                HeightRequest = 38,
                StrokeThickness = 0,
                WidthRequest = 44,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
                {
                    CornerRadius = 8
                },
                Content = new Label
                {
                    FontAttributes = FontAttributes.Bold,
                    FontFamily = "OpenSansSemibold",
                    FontSize = 13,
                    HorizontalTextAlignment = TextAlignment.Center,
                    Text = summary.Level,
                    TextColor = Color.FromArgb("#2563EB"),
                    VerticalTextAlignment = TextAlignment.Center
                }
            };
            row.Children.Add(levelBadge);

            var progressStack = new VerticalStackLayout
            {
                Spacing = 5,
                VerticalOptions = LayoutOptions.Center
            };
            var progressHeader = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 8
            };
            progressHeader.Children.Add(new Label
            {
                FontSize = 11,
                LineBreakMode = LineBreakMode.TailTruncation,
                Text = "Progress",
                TextColor = Color.FromArgb("#777777")
            });
            var percentLabel = new Label
            {
                FontAttributes = FontAttributes.Bold,
                FontFamily = "OpenSansSemibold",
                FontSize = 11,
                HorizontalTextAlignment = TextAlignment.End,
                Text = $"{percent}%",
                TextColor = Color.FromArgb("#2E7D32")
            };
            Grid.SetColumn(percentLabel, 1);
            progressHeader.Children.Add(percentLabel);
            progressStack.Children.Add(progressHeader);
            progressStack.Children.Add(new ProgressBar
            {
                BackgroundColor = Color.FromArgb("#EAF6EA"),
                Progress = progress,
                ProgressColor = Color.FromArgb("#2E7D32")
            });
            Grid.SetColumn(progressStack, 1);
            row.Children.Add(progressStack);

            var masteredStack = new VerticalStackLayout
            {
                HorizontalOptions = LayoutOptions.End,
                Spacing = 0,
                VerticalOptions = LayoutOptions.Center
            };
            masteredStack.Children.Add(new Label
            {
                FontAttributes = FontAttributes.Bold,
                FontFamily = "OpenSansSemibold",
                FontSize = 20,
                HorizontalTextAlignment = TextAlignment.End,
                Text = summary.MasteredCount.ToString(),
                TextColor = Color.FromArgb("#2E7D32")
            });
            masteredStack.Children.Add(new Label
            {
                FontSize = 10,
                HorizontalTextAlignment = TextAlignment.End,
                Text = $"von {summary.ActiveCount} W\u00f6rtern",
                TextColor = Color.FromArgb("#777777")
            });
            Grid.SetColumn(masteredStack, 2);
            row.Children.Add(masteredStack);

            return row;
        }

        private void ApplyWordOfTheDay(WordEntry? word)
        {
            wordOfTheDay = word;
            WordOfDayCard.IsVisible = word is not null;
            if (word is null)
            {
                return;
            }

            var isNoun =
                string.Equals(word.Type, "noun", StringComparison.OrdinalIgnoreCase);
            var article = isNoun ? GetArticle(word.Gender) : null;
            var visualStyle = WordVisualStyleResolver.Resolve(word);

            WordOfDayWordLabel.Text = string.IsNullOrEmpty(article)
                ? word.Word
                : $"{article} {word.Word}";
            WordOfDayWordLabel.TextColor = visualStyle.AccentColor;
            WordOfDayTypeLabel.Text = word.Type.ToUpperInvariant();
            WordOfDayTypeLabel.TextColor = visualStyle.AccentColor;
            WordOfDayTypeBadge.BackgroundColor = visualStyle.BackgroundColor;
            WordOfDayTypeBadge.Stroke = visualStyle.StrokeColor;

            var hasPlural = isNoun && !string.IsNullOrWhiteSpace(word.Plural);
            WordOfDayPluralLabel.IsVisible = hasPlural;
            WordOfDayPluralLabel.Text = hasPlural ? $"die {word.Plural}" : string.Empty;
            WordOfDayTranslationLabel.Text = word.Translation;
        }

        private async void OnFocusPracticeTapped(object sender, TappedEventArgs e)
        {
            if (isVocabularyCompletionFocus)
            {
                settingsService.VocabularyDirection = focusVocabularyDirection;
            }

            await NavigateToModeAsync(focusMode);
        }

        private async void OnMasteryOverviewTapped(object sender, TappedEventArgs e) =>
            await ShowLevelMasteryPopupAsync();

        private async void OnVocabularyTapped(object sender, TappedEventArgs e) =>
            await NavigateToModeAsync(PracticeMode.Meaning);

        private async void OnArticlesTapped(object sender, TappedEventArgs e) =>
            await NavigateToModeAsync(PracticeMode.Article);

        private async void OnPluralsTapped(object sender, TappedEventArgs e) =>
            await NavigateToModeAsync(PracticeMode.Plural);

        private async void OnIrregularVerbsTapped(object sender, TappedEventArgs e) =>
            await NavigateToModeAsync(PracticeMode.IrregularVerb);

        private async void OnReviewTapped(object sender, TappedEventArgs e) =>
            await NavigateToModeAsync(reviewMode);

        private async void OnWordOfDayTapped(object sender, TappedEventArgs e)
        {
            if (wordOfTheDay is null)
            {
                return;
            }

            await Shell.Current.GoToAsync(
                $"{nameof(WordDetailsPage)}?wordId={wordOfTheDay.Id}");
        }

        private async void OnAvatarTapped(object sender, TappedEventArgs e) =>
            await HandleProfileTappedAsync();

        private async void OnUserNameTapped(object sender, TappedEventArgs e) =>
            await HandleProfileTappedAsync();

        private async Task HandleProfileTappedAsync()
        {
            if (googleAuthService.IsSignedIn || isGoogleSignInBusy)
            {
                return;
            }

            var shouldSetUp = await Shell.Current.DisplayAlert(
                "Google account",
                "Google account is not set up. Do you want to set up Google account?",
                "Set up",
                "Cancel");
            if (!shouldSetUp)
            {
                return;
            }

            isGoogleSignInBusy = true;
            SetGoogleSignInOverlayVisible(true);
            try
            {
                await googleAuthService.SignInAsync();
                ApplySignedInUser();
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert(
                    "Google sign-in failed",
                    ex.Message,
                    "OK");
            }
            finally
            {
                isGoogleSignInBusy = false;
                SetGoogleSignInOverlayVisible(false);
                ApplySignedInUser();
            }
        }

        private void OnCancelGoogleSignInClicked(object sender, EventArgs e) =>
            googleAuthService.CancelPendingSignIn();

        private void SetGoogleSignInOverlayVisible(bool isVisible)
        {
            GoogleSignInOverlay.IsVisible = isVisible;
            GoogleSignInActivityIndicator.IsRunning = isVisible;
            CancelGoogleSignInButton.IsVisible =
                isVisible && DeviceInfo.Current.Platform == DevicePlatform.WinUI;
        }

        private async Task ShowLevelMasteryPopupAsync()
        {
            var rows = new VerticalStackLayout { Spacing = 8 };
            ApplyLevelMasteryRows(
                rows,
                levelMasterySummaries,
                includeEmptyMessage: true);

            var popupPage = new ContentPage
            {
                BackgroundColor = Color.FromArgb("#99000000"),
                Padding = new Thickness(24)
            };

            var closeButton = new Border
            {
                BackgroundColor = Colors.White,
                HeightRequest = 34,
                HorizontalOptions = LayoutOptions.End,
                Padding = new Thickness(0),
                Stroke = Color.FromArgb("#DDE8F5"),
                StrokeThickness = 1,
                VerticalOptions = LayoutOptions.Start,
                WidthRequest = 34,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
                {
                    CornerRadius = 8
                },
                Content = new Microsoft.Maui.Controls.Shapes.Path
                {
                    Aspect = Stretch.Uniform,
                    Data = new Microsoft.Maui.Controls.Shapes.PathGeometryConverter()
                        .ConvertFromInvariantString("M 9 9 L 23 23 M 23 9 L 9 23") as Microsoft.Maui.Controls.Shapes.Geometry,
                    Fill = Colors.Transparent,
                    HeightRequest = 18,
                    HorizontalOptions = LayoutOptions.Center,
                    Stroke = Color.FromArgb("#171717"),
                    StrokeLineCap = Microsoft.Maui.Controls.Shapes.PenLineCap.Round,
                    StrokeThickness = 2.2,
                    VerticalOptions = LayoutOptions.Center,
                    WidthRequest = 18
                }
            };
            ToolTipProperties.SetText(closeButton, "Close");
            SemanticProperties.SetDescription(closeButton, "Close");
            var closeTap = new TapGestureRecognizer();
            closeTap.Tapped += async (_, _) =>
                await popupPage.Navigation.PopModalAsync(animated: false);
            closeButton.GestureRecognizers.Add(closeTap);

            var titleStack = new VerticalStackLayout
            {
                Spacing = 2,
                Children =
                {
                    new Label
                    {
                        FontAttributes = FontAttributes.Bold,
                        FontFamily = "OpenSansSemibold",
                        FontSize = 18,
                        Text = "Mastered by level",
                        TextColor = Color.FromArgb("#171717")
                    },
                    new Label
                    {
                        FontSize = 12,
                        Text = "Only levels with learning activity",
                        TextColor = Color.FromArgb("#777777")
                    }
                }
            };
            var header = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 12
            };
            header.Children.Add(titleStack);
            Grid.SetColumn(closeButton, 1);
            header.Children.Add(closeButton);

            var card = new Border
            {
                BackgroundColor = Colors.White,
                HorizontalOptions = LayoutOptions.Fill,
                MaximumWidthRequest = 420,
                Padding = new Thickness(18),
                Stroke = Color.FromArgb("#DDE8F5"),
                StrokeThickness = 1,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
                {
                    CornerRadius = 8
                },
                VerticalOptions = LayoutOptions.Center,
                Content = new VerticalStackLayout
                {
                    Spacing = 16,
                    Children =
                    {
                        header,
                        rows
                    }
                }
            };

            popupPage.Content = new Grid
            {
                Children = { card }
            };

            await Shell.Current.Navigation.PushModalAsync(popupPage, animated: false);
        }

        private static Task NavigateToModeAsync(PracticeMode mode) =>
            Shell.Current.GoToAsync(
                mode switch
                {
                    PracticeMode.Article => nameof(ArticlesStartPage),
                    PracticeMode.Plural => nameof(PluralsStartPage),
                    PracticeMode.IrregularVerb => nameof(IrregularVerbsStartPage),
                    _ => nameof(VocabularyStartPage)
                });

        private static bool IsFreshStart(
            IReadOnlyDictionary<PracticeMode, PracticeModeSummary> summaries,
            int masteredCount) =>
            masteredCount == 0 &&
            summaries.Values.All(summary => summary.LearnedCount == 0);

        private static void ApplyModeSummary(
            Label percentLabel,
            Label learnedLabel,
            PracticeModeSummary summary)
        {
            percentLabel.Text = $"{summary.LearnedPercent}%";
            learnedLabel.Text =
                $"{summary.LearnedCount} / {summary.TotalCount} learned";
        }

        private static (PracticeMode Mode, PracticeModeSummary Summary) SelectFocusMode(
            IReadOnlyDictionary<PracticeMode, PracticeModeSummary> summaries,
            int testChunkSize)
        {
            var vocabularySummary = summaries[PracticeMode.Meaning];
            if (HasEnoughVocabularyCompletionWords(
                    vocabularySummary,
                    testChunkSize))
            {
                return (PracticeMode.Meaning, vocabularySummary);
            }

            var advancedFocus = summaries
                .Where(pair => pair.Key != PracticeMode.Meaning)
                .Where(pair => HasEnoughVocabularyMasteredWords(pair.Value, testChunkSize))
                .OrderBy(pair => pair.Value.Progress)
                .ThenByDescending(pair => GetVocabularyMasteredRatio(pair.Value))
                .ThenByDescending(pair => pair.Value.MistakeCount)
                .ThenBy(pair => pair.Key)
                .Select(pair => ((PracticeMode Mode, PracticeModeSummary Summary)?)(
                    pair.Key,
                    pair.Value))
                .FirstOrDefault();

            return advancedFocus ??
                (PracticeMode.Meaning, summaries[PracticeMode.Meaning]);
        }

        private static bool HasEnoughVocabularyCompletionWords(
            PracticeModeSummary summary,
            int testChunkSize)
        {
            var requiredCount = Math.Max(1, testChunkSize);
            var largestDirectionBacklog = Math.Max(
                summary.GermanToEnglishCompletionCount,
                summary.EnglishToGermanCompletionCount);
            return largestDirectionBacklog >= requiredCount;
        }

        private static VocabularyTestDirection SelectVocabularyCompletionDirection(
            PracticeModeSummary summary,
            VocabularyTestDirection currentDirection)
        {
            if (summary.GermanToEnglishCompletionCount >
                summary.EnglishToGermanCompletionCount)
            {
                return VocabularyTestDirection.GermanToEnglish;
            }

            if (summary.EnglishToGermanCompletionCount >
                summary.GermanToEnglishCompletionCount)
            {
                return VocabularyTestDirection.EnglishToGerman;
            }

            return currentDirection;
        }

        private static int GetVocabularyCompletionCount(
            PracticeModeSummary summary,
            VocabularyTestDirection direction) =>
            direction == VocabularyTestDirection.GermanToEnglish
                ? summary.GermanToEnglishCompletionCount
                : summary.EnglishToGermanCompletionCount;

        private static string GetVocabularyCompletionDescription(
            VocabularyTestDirection direction,
            int count)
        {
            var wordLabel = count == 1 ? "word is" : "words are";
            var directionLabel =
                direction == VocabularyTestDirection.GermanToEnglish
                    ? "German-to-English"
                    : "English-to-German";

            return $"{count} {wordLabel} one direction away from mastery. " +
                   $"Let's finish the {directionLabel} side and lock " +
                   $"{(count == 1 ? "it" : "them")} in.";
        }

        private static bool HasEnoughVocabularyMasteredWords(
            PracticeModeSummary summary,
            int testChunkSize)
        {
            var requiredCount = Math.Max(1, testChunkSize);
            return summary.VocabularyMasteredRemainingCount >= requiredCount;
        }

        private static double GetVocabularyMasteredRatio(PracticeModeSummary summary) =>
            summary.RemainingCount == 0
                ? 0
                : (double)summary.VocabularyMasteredRemainingCount / summary.RemainingCount;

        private static string GetAdvancedFocusDescription(
            PracticeMode mode,
            int masteredVocabularyCount)
        {
            var nextStep = mode switch
            {
                PracticeMode.Article => "Next mission: make articles second nature.",
                PracticeMode.Plural => "Next mission: turn singulars into plurals with confidence.",
                PracticeMode.IrregularVerb => "Next mission: make irregular forms feel effortless.",
                _ => "Let's keep that momentum going."
            };

            return $"You've mastered {masteredVocabularyCount} words in both directions. " +
                   nextStep;
        }

        private static string FormatMistakeCount(int count) =>
            count == 1 ? "1 Fehler" : $"{count} Fehler";

        private static string GetGermanModeName(PracticeMode mode) =>
            mode switch
            {
                PracticeMode.Article => "Artikel",
                PracticeMode.Plural => "Plural",
                PracticeMode.IrregularVerb => "Unregelmäßige Verben",
                _ => "Wortschatz"
            };

        private static string GetEnglishModeName(PracticeMode mode) =>
            mode switch
            {
                PracticeMode.Article => "Articles",
                PracticeMode.Plural => "Plurals",
                PracticeMode.IrregularVerb => "Irregular verbs",
                _ => "Vocabulary"
            };

        private static string GetGreeting(int hour) =>
            hour switch
            {
                >= 5 and < 11 => "Guten Morgen",
                >= 11 and < 18 => "Guten Tag",
                >= 18 and < 23 => "Guten Abend",
                _ => "Gute Nacht"
            };

        private static string? GetArticle(string? gender) =>
            gender?.ToLowerInvariant() switch
            {
                "m" => "der",
                "f" => "die",
                "n" => "das",
                _ => null
            };
    }
}
