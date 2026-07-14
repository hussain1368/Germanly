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
        private const string AutoBackupEnabledKey = "Backup.AutoBackupEnabled";
        private const string BackupNeededKey = "Backup.BackupNeeded";
        private const string UserGuideSeenKey = "UserGuide.HasSeen";

        public int LearnedThreshold
        {
            get => Preferences.Default.Get(LearnedThresholdKey, 3);
            set => SetIntSetting(LearnedThresholdKey, value, 3);
        }

        public int TestChunkSize
        {
            get => Preferences.Default.Get(TestChunkSizeKey, 10);
            set => SetIntSetting(TestChunkSizeKey, value, 10);
        }

        public bool SoundsEnabled
        {
            get => Preferences.Default.Get(SoundsEnabledKey, true);
            set => SetBooleanSetting(SoundsEnabledKey, value, true);
        }

        public bool VibrationsEnabled
        {
            get => Preferences.Default.Get(VibrationsEnabledKey, true);
            set => SetBooleanSetting(VibrationsEnabledKey, value, true);
        }

        public string VocabularyLevel
        {
            get => NormalizeLevel(Preferences.Default.Get(VocabularyLevelKey, string.Empty));
            set => SetStringSetting(VocabularyLevelKey, NormalizeLevel(value), string.Empty);
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
            set => SetStringSetting(
                VocabularyDirectionKey,
                value.ToString(),
                nameof(VocabularyTestDirection.GermanToEnglish));
        }

        public string ArticleLevel
        {
            get => NormalizeLevel(Preferences.Default.Get(ArticleLevelKey, string.Empty));
            set => SetStringSetting(ArticleLevelKey, NormalizeLevel(value), string.Empty);
        }

        public ArticleCase SelectedArticleCase
        {
            get => GetEnumPreference(ArticleCaseKey, ArticleCase.Nominative);
            set => SetStringSetting(ArticleCaseKey, value.ToString(), nameof(ArticleCase.Nominative));
        }

        public ArticleType SelectedArticleType
        {
            get => GetEnumPreference(ArticleTypeKey, ArticleType.Definite);
            set => SetStringSetting(ArticleTypeKey, value.ToString(), nameof(ArticleType.Definite));
        }

        public string PluralLevel
        {
            get => NormalizeLevel(Preferences.Default.Get(PluralLevelKey, string.Empty));
            set => SetStringSetting(PluralLevelKey, NormalizeLevel(value), string.Empty);
        }

        public IrregularTestMethod SelectedPluralTestMethod
        {
            get => GetEnumPreference(PluralTestMethodKey, IrregularTestMethod.MultipleChoice);
            set => SetStringSetting(
                PluralTestMethodKey,
                value.ToString(),
                nameof(IrregularTestMethod.MultipleChoice));
        }

        public IrregularVerbForm SelectedIrregularVerbForm
        {
            get => GetEnumPreference(IrregularVerbFormKey, IrregularVerbForm.Prateritum);
            set => SetStringSetting(
                IrregularVerbFormKey,
                value.ToString(),
                nameof(IrregularVerbForm.Prateritum));
        }

        public string IrregularVerbLevel
        {
            get => NormalizeLevel(Preferences.Default.Get(IrregularVerbLevelKey, string.Empty));
            set => SetStringSetting(IrregularVerbLevelKey, NormalizeLevel(value), string.Empty);
        }

        public IrregularTestMethod SelectedIrregularTestMethod
        {
            get => GetEnumPreference(IrregularTestMethodKey, IrregularTestMethod.MultipleChoice);
            set => SetStringSetting(
                IrregularTestMethodKey,
                value.ToString(),
                nameof(IrregularTestMethod.MultipleChoice));
        }

        public bool AutoBackupEnabled
        {
            get => Preferences.Default.Get(AutoBackupEnabledKey, false);
            set => SetBooleanSetting(AutoBackupEnabledKey, value, false);
        }

        public bool BackupNeeded => Preferences.Default.Get(BackupNeededKey, false);

        public void MarkBackupNeeded() =>
            Preferences.Default.Set(BackupNeededKey, true);

        public void ClearBackupNeeded() =>
            Preferences.Default.Set(BackupNeededKey, false);

        public bool HasSeenUserGuide
        {
            get => Preferences.Default.Get(UserGuideSeenKey, false);
            set => Preferences.Default.Set(UserGuideSeenKey, value);
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

        private void SetIntSetting(string key, int value, int defaultValue)
        {
            if (Preferences.Default.Get(key, defaultValue) == value)
            {
                return;
            }

            Preferences.Default.Set(key, value);
            MarkBackupNeeded();
        }

        private void SetBooleanSetting(string key, bool value, bool defaultValue)
        {
            if (Preferences.Default.Get(key, defaultValue) == value)
            {
                return;
            }

            Preferences.Default.Set(key, value);
            MarkBackupNeeded();
        }

        private void SetStringSetting(string key, string value, string defaultValue)
        {
            if (string.Equals(
                Preferences.Default.Get(key, defaultValue),
                value,
                StringComparison.Ordinal))
            {
                return;
            }

            Preferences.Default.Set(key, value);
            MarkBackupNeeded();
        }
    }
}
