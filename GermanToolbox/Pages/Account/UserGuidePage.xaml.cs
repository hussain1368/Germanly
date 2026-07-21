namespace GermanToolbox
{
    public sealed class UserGuideSlide
    {
        public required string Image { get; init; }

        public required string Description { get; init; }
    }

    public partial class UserGuidePage : ContentPage
    {
        public IReadOnlyList<UserGuideSlide> Slides { get; } =
        [
            new()
            {
                Image = "slide1.jpg",
                Description =
                    "The Home page gives you a quick view of your vocabulary, learning progress, and the best next action."
            },
            new()
            {
                Image = "slide2.jpg",
                Description =
                    "Search German words or translations, then open a word to see its meaning, article, plural, and verb information."
            },
            new()
            {
                Image = "slide3.jpg",
                Description =
                    "Practice meanings, articles, plurals, and irregular verbs. Each mode focuses on a different part of German vocabulary."
            },
            new()
            {
                Image = "slide4.jpg",
                Description =
                    "Correct answers raise a word's score. Wrong answers lower it and keep difficult words available for mistake review."
            }
        ];

        public UserGuidePage()
        {
            InitializeComponent();
            BindingContext = this;
            UpdateNavigation(0);
        }

        private async void OnSkipClicked(object sender, EventArgs e) =>
            await CloseGuideAsync();

        private void OnPositionChanged(object sender, PositionChangedEventArgs e) =>
            UpdateNavigation(e.CurrentPosition);

        private async void OnNextClicked(object sender, EventArgs e)
        {
            if (GuideCarousel.Position < Slides.Count - 1)
            {
                GuideCarousel.Position++;
                return;
            }

            await CloseGuideAsync();
        }

        private void UpdateNavigation(int position)
        {
            NextButton.Text = position == Slides.Count - 1 ? "Loslegen" : "Weiter";
        }

        private async Task CloseGuideAsync()
        {
            var settingsService = AppServices.GetRequiredService<PracticeSettingsService>();
            var googleAuthService = AppServices.GetRequiredService<GoogleAuthService>();
            var isFirstRun = !settingsService.HasSeenUserGuide;
            settingsService.HasSeenUserGuide = true;

            if (isFirstRun && !googleAuthService.IsSignedIn)
            {
                await Shell.Current.GoToAsync($"../{nameof(GoogleSetupPage)}");
                return;
            }

            if (isFirstRun)
            {
                settingsService.HasSeenGoogleSetupPrompt = true;
            }

            await Shell.Current.GoToAsync("..");
        }
    }
}
