namespace GermanToolbox
{
    public sealed class TestSession
    {
        public TestSession(
            PracticeMode mode,
            bool isMistakeReview,
            IReadOnlyList<WordEntry> words,
            VocabularyTestDirection vocabularyDirection = VocabularyTestDirection.GermanToEnglish,
            ArticleCase articleCase = ArticleCase.Nominative,
            ArticleType articleType = ArticleType.Definite,
            IrregularVerbForm irregularVerbForm = IrregularVerbForm.Prateritum,
            IrregularTestMethod irregularTestMethod = IrregularTestMethod.MultipleChoice)
        {
            Mode = mode;
            IsMistakeReview = isMistakeReview;
            VocabularyDirection = vocabularyDirection;
            ArticleCase = articleCase;
            ArticleType = articleType;
            IrregularVerbForm = irregularVerbForm;
            IrregularTestMethod = irregularTestMethod;
            Words = words.Select(word => new TestSessionWord(word)).ToList();
            StartedAt = DateTimeOffset.Now;
        }

        public PracticeMode Mode { get; }

        public bool IsMistakeReview { get; }

        public VocabularyTestDirection VocabularyDirection { get; }

        public ArticleCase ArticleCase { get; }

        public ArticleType ArticleType { get; }

        public IrregularVerbForm IrregularVerbForm { get; }

        public IrregularTestMethod IrregularTestMethod { get; }

        public DateTimeOffset StartedAt { get; }

        public List<TestSessionWord> Words { get; }

        public int CurrentIndex { get; private set; }

        public int TotalCount => Words.Count;

        public bool IsFinished => CurrentIndex >= Words.Count;

        public TestSessionWord? CurrentWord => IsFinished ? null : Words[CurrentIndex];

        public void MoveNext()
        {
            CurrentIndex++;
        }
    }
}
