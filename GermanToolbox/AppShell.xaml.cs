namespace GermanToolbox
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute(nameof(ArticlesStartPage), typeof(ArticlesStartPage));
            Routing.RegisterRoute(nameof(ArticlesTestPage), typeof(ArticlesTestPage));
            Routing.RegisterRoute(nameof(ArticlesResultsPage), typeof(ArticlesResultsPage));
            Routing.RegisterRoute(nameof(VocabularyStartPage), typeof(VocabularyStartPage));
            Routing.RegisterRoute(nameof(VocabularyTestPage), typeof(VocabularyTestPage));
            Routing.RegisterRoute(nameof(VocabularyResultsPage), typeof(VocabularyResultsPage));
            Routing.RegisterRoute(nameof(PluralsStartPage), typeof(PluralsStartPage));
            Routing.RegisterRoute(nameof(PluralsTestPage), typeof(PluralsTestPage));
            Routing.RegisterRoute(nameof(PluralsResultsPage), typeof(PluralsResultsPage));
            Routing.RegisterRoute(nameof(IrregularVerbsStartPage), typeof(IrregularVerbsStartPage));
            Routing.RegisterRoute(nameof(IrregularVerbsTestPage), typeof(IrregularVerbsTestPage));
            Routing.RegisterRoute(nameof(IrregularVerbsResultsPage), typeof(IrregularVerbsResultsPage));
            Routing.RegisterRoute(nameof(WordDetailsPage), typeof(WordDetailsPage));
            Routing.RegisterRoute(nameof(PracticeRulesPage), typeof(PracticeRulesPage));
            Routing.RegisterRoute(nameof(UserGuidePage), typeof(UserGuidePage));
            Routing.RegisterRoute(nameof(SignInPage), typeof(SignInPage));
            Routing.RegisterRoute(nameof(VerificationCodePage), typeof(VerificationCodePage));
            Routing.RegisterRoute(nameof(UserProfilePage), typeof(UserProfilePage));
        }
    }
}
