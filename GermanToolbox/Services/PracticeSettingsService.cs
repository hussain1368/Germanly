using Microsoft.Maui.Storage;

namespace GermanToolbox
{
    public sealed class PracticeSettingsService
    {
        private const string LearnedThresholdKey = "Practice.LearnedThreshold";
        private const string TestChunkSizeKey = "Practice.TestChunkSize";
        private const string SoundsEnabledKey = "Feedback.SoundsEnabled";
        private const string VibrationsEnabledKey = "Feedback.VibrationsEnabled";
        private const string VocabularyLevelKey = "Vocabulary.SelectedLevel";
        private const string VocabularyDirectionKey = "Vocabulary.SelectedDirection";
        private const string ArticleLevelKey = "Articles.SelectedLevel";
        private const string ArticleCaseKey = "Articles.SelectedCase";
        private const string ArticleTypeKey = "Articles.SelectedType";
        private const string PluralLevelKey = "Plurals.SelectedLevel";
        private const string PluralTestMethodKey = "Plurals.SelectedMethod";
        private const string IrregularVerbLevelKey = "IrregularVerbs.SelectedLevel";
        private const string IrregularVerbFormKey = "IrregularVerbs.SelectedForm";
        private const string IrregularTestMethodKey = "IrregularVerbs.SelectedMethod";

        public int LearnedThreshold
        {
            get => Preferences.Default.Get(LearnedThresholdKey, 3);
            set => Preferences.Default.Set(LearnedThresholdKey, value);
        }

        public int TestChunkSize
        {
            get => Preferences.Default.Get(TestChunkSizeKey, 10);
            set => Preferences.Default.Set(TestChunkSizeKey, value);
        }

        public bool SoundsEnabled
        {
            get => Preferences.Default.Get(SoundsEnabledKey, true);
            set => Preferences.Default.Set(SoundsEnabledKey, value);
        }

        public bool VibrationsEnabled
        {
            get => Preferences.Default.Get(VibrationsEnabledKey, true);
            set => Preferences.Default.Set(VibrationsEnabledKey, value);
        }

        public string VocabularyLevel
        {
            get => NormalizeLevel(Preferences.Default.Get(VocabularyLevelKey, string.Empty));
            set => Preferences.Default.Set(VocabularyLevelKey, NormalizeLevel(value));
        }

        public VocabularyTestDirection VocabularyDirection
        {
            get
            {
                var rawDirection = Preferences.Default.Get(
                    VocabularyDirectionKey,
                    nameof(VocabularyTestDirection.GermanToEnglish));

                return Enum.TryParse<VocabularyTestDirection>(rawDirection, out var direction)
                    ? direction
                    : VocabularyTestDirection.GermanToEnglish;
            }
            set => Preferences.Default.Set(VocabularyDirectionKey, value.ToString());
        }

        public string ArticleLevel
        {
            get => NormalizeLevel(Preferences.Default.Get(ArticleLevelKey, string.Empty));
            set => Preferences.Default.Set(ArticleLevelKey, NormalizeLevel(value));
        }

        public ArticleCase SelectedArticleCase
        {
            get => GetEnumPreference(ArticleCaseKey, ArticleCase.Nominative);
            set => Preferences.Default.Set(ArticleCaseKey, value.ToString());
        }

        public ArticleType SelectedArticleType
        {
            get => GetEnumPreference(ArticleTypeKey, ArticleType.Definite);
            set => Preferences.Default.Set(ArticleTypeKey, value.ToString());
        }

        public string PluralLevel
        {
            get => NormalizeLevel(Preferences.Default.Get(PluralLevelKey, string.Empty));
            set => Preferences.Default.Set(PluralLevelKey, NormalizeLevel(value));
        }

        public IrregularTestMethod SelectedPluralTestMethod
        {
            get => GetEnumPreference(PluralTestMethodKey, IrregularTestMethod.MultipleChoice);
            set => Preferences.Default.Set(PluralTestMethodKey, value.ToString());
        }

        public IrregularVerbForm SelectedIrregularVerbForm
        {
            get => GetEnumPreference(IrregularVerbFormKey, IrregularVerbForm.Prateritum);
            set => Preferences.Default.Set(IrregularVerbFormKey, value.ToString());
        }

        public string IrregularVerbLevel
        {
            get => NormalizeLevel(Preferences.Default.Get(IrregularVerbLevelKey, string.Empty));
            set => Preferences.Default.Set(IrregularVerbLevelKey, NormalizeLevel(value));
        }

        public IrregularTestMethod SelectedIrregularTestMethod
        {
            get => GetEnumPreference(IrregularTestMethodKey, IrregularTestMethod.MultipleChoice);
            set => Preferences.Default.Set(IrregularTestMethodKey, value.ToString());
        }

        private static string NormalizeLevel(string level)
        {
            var normalizedLevel = level.Trim().ToUpperInvariant();
            return normalizedLevel switch
            {
                "A1" or "A2" or "B1" or "B2" or "C1" => normalizedLevel,
                _ => string.Empty
            };
        }

        private static TEnum GetEnumPreference<TEnum>(string key, TEnum defaultValue)
            where TEnum : struct, Enum
        {
            var rawValue = Preferences.Default.Get(key, defaultValue.ToString());
            return Enum.TryParse<TEnum>(rawValue, out var value) ? value : defaultValue;
        }
    }
}
