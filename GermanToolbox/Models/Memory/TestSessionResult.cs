namespace GermanToolbox
{
    public sealed class TestSessionResult
    {
        public TestSessionResult(
            PracticeMode mode,
            int totalCount,
            int correctCount,
            int mistakeCount,
            TimeSpan duration,
            IReadOnlyList<WordEntry> mistakenWords,
            VocabularyTestDirection vocabularyDirection = VocabularyTestDirection.GermanToEnglish,
            ArticleCase articleCase = ArticleCase.Nominative,
            ArticleType articleType = ArticleType.Definite,
            IrregularVerbForm irregularVerbForm = IrregularVerbForm.Prateritum,
            IrregularTestMethod irregularTestMethod = IrregularTestMethod.MultipleChoice)
        {
            Mode = mode;
            TotalCount = totalCount;
            CorrectCount = correctCount;
            MistakeCount = mistakeCount;
            Duration = duration;
            MistakenWords = mistakenWords;
            VocabularyDirection = vocabularyDirection;
            ArticleCase = articleCase;
            ArticleType = articleType;
            IrregularVerbForm = irregularVerbForm;
            IrregularTestMethod = irregularTestMethod;
        }

        public PracticeMode Mode { get; }

        public int TotalCount { get; }

        public int CorrectCount { get; }

        public int MistakeCount { get; }

        public TimeSpan Duration { get; }

        public IReadOnlyList<WordEntry> MistakenWords { get; }

        public VocabularyTestDirection VocabularyDirection { get; }

        public ArticleCase ArticleCase { get; }

        public ArticleType ArticleType { get; }

        public IrregularVerbForm IrregularVerbForm { get; }

        public IrregularTestMethod IrregularTestMethod { get; }

        public double Accuracy => TotalCount == 0 ? 0 : (double)CorrectCount / TotalCount;

        public int AccuracyPercent => (int)Math.Round(Accuracy * 100);
    }
}
