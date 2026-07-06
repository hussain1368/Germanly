namespace GermanToolbox
{
    public sealed class UserGuideSlide
    {
        public required string Symbol { get; init; }

        public required string Description { get; init; }

        public required Color AccentColor { get; init; }

        public required Color AccentWash { get; init; }

        public required Color IllustrationBackground { get; init; }

        public required Color IllustrationStroke { get; init; }
    }

    public partial class UserGuidePage : ContentPage
    {
        public IReadOnlyList<UserGuideSlide> Slides { get; } =
        [
            new()
            {
                Symbol = "GT",
                Description =
                    "The Home page gives you a quick view of your vocabulary, learning progress, and the best next action.",
                AccentColor = Color.FromArgb("#2563EB"),
                AccentWash = Color.FromArgb("#DCEAFF"),
                IllustrationBackground = Color.FromArgb("#F2F7FF"),
                IllustrationStroke = Color.FromArgb("#CFE0FA")
            },
            new()
            {
                Symbol = "Aa",
                Description =
                    "Search German words or translations, then open a word to see its meaning, article, plural, and verb information.",
                AccentColor = Color.FromArgb("#7C3AED"),
                AccentWash = Color.FromArgb("#EDE4FF"),
                IllustrationBackground = Color.FromArgb("#F8F4FF"),
                IllustrationStroke = Color.FromArgb("#DDD0F5")
            },
            new()
            {
                Symbol = "4×",
                Description =
                    "Practice meanings, articles, plurals, and irregular verbs. Each mode focuses on a different part of German vocabulary.",
                AccentColor = Color.FromArgb("#D97706"),
                AccentWash = Color.FromArgb("#FCE8C6"),
                IllustrationBackground = Color.FromArgb("#FFF9EE"),
                IllustrationStroke = Color.FromArgb("#F2D7A7")
            },
            new()
            {
                Symbol = "+1",
                Description =
                    "Correct answers raise a word's score. Wrong answers lower it and keep difficult words available for mistake review.",
                AccentColor = Color.FromArgb("#16835D"),
                AccentWash = Color.FromArgb("#D8F1E7"),
                IllustrationBackground = Color.FromArgb("#F1FAF6"),
                IllustrationStroke = Color.FromArgb("#CBE8DC")
            },
            new()
            {
                Symbol = "⚙",
                Description =
                    "Choose the session size and learning threshold, and control answer sounds and vibration from Settings.",
                AccentColor = Color.FromArgb("#D43D35"),
                AccentWash = Color.FromArgb("#F8DDDA"),
                IllustrationBackground = Color.FromArgb("#FFF5F4"),
                IllustrationStroke = Color.FromArgb("#F0D2CF")
            }
        ];

        public UserGuidePage()
        {
            InitializeComponent();
            BindingContext = this;
            UpdateNavigation(0);
        }

        private async void OnCloseTapped(object sender, TappedEventArgs e) =>
            await CloseGuideAsync();

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
            ProgressLabel.Text = $"{position + 1} von {Slides.Count}";
            NextButton.Text = position == Slides.Count - 1 ? "Loslegen" : "Weiter";
        }

        private static Task CloseGuideAsync() => Shell.Current.GoToAsync("..");
    }
}
