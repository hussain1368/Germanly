namespace GermanToolbox
{
    public partial class PracticeRulesPage : ContentPage
    {
        private readonly Color selectedBackground = Color.FromArgb("#EAF1FF");
        private readonly Color selectedStroke = Color.FromArgb("#2563EB");
        private readonly Color selectedText = Color.FromArgb("#2563EB");
        private readonly Color unselectedBackground = Colors.White;
        private readonly Color unselectedStroke = Color.FromArgb("#E4E4DE");
        private readonly Color unselectedText = Color.FromArgb("#171717");

        public PracticeRulesPage()
        {
            InitializeComponent();
            SelectTab(PracticeMode.Article);
        }

        private async void OnBackTapped(object sender, TappedEventArgs e) =>
            await Shell.Current.GoToAsync("..");

        private void OnArticlesTapped(object sender, TappedEventArgs e) =>
            SelectTab(PracticeMode.Article);

        private void OnPluralsTapped(object sender, TappedEventArgs e) =>
            SelectTab(PracticeMode.Plural);

        private void OnIrregularTapped(object sender, TappedEventArgs e) =>
            SelectTab(PracticeMode.IrregularVerb);

        private void SelectTab(PracticeMode mode)
        {
            ResetTab(ArticlesTab, ArticlesTabLabel);
            ResetTab(PluralsTab, PluralsTabLabel);
            ResetTab(IrregularTab, IrregularTabLabel);

            var (tab, label, title, body) = mode switch
            {
                PracticeMode.Article => (
                    ArticlesTab,
                    ArticlesTabLabel,
                    "Artikel",
                    "Dummy article rule text. Explain der, die, das patterns and examples here later."),
                PracticeMode.Plural => (
                    PluralsTab,
                    PluralsTabLabel,
                    "Plural",
                    "Dummy plural rule text. Explain common plural endings and practice hints here later."),
                PracticeMode.IrregularVerb => (
                    IrregularTab,
                    IrregularTabLabel,
                    "Unregelmäßige Verben",
                    "Dummy irregular verb rule text. Explain stem changes and tense forms here later."),
                _ => (
                    ArticlesTab,
                    ArticlesTabLabel,
                    "Artikel",
                    "Dummy article rule text. Explain der, die, das patterns and examples here later.")
            };

            tab.BackgroundColor = selectedBackground;
            tab.Stroke = selectedStroke;
            tab.StrokeThickness = 2;
            label.TextColor = selectedText;
            RuleTitleLabel.Text = title;
            RuleBodyLabel.Text = body;
        }

        private void ResetTab(Border tab, Label label)
        {
            tab.BackgroundColor = unselectedBackground;
            tab.Stroke = unselectedStroke;
            tab.StrokeThickness = 1;
            label.TextColor = unselectedText;
        }
    }
}
