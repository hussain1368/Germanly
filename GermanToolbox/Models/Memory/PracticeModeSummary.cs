namespace GermanToolbox
{
    public sealed class PracticeModeSummary
    {
        public PracticeModeSummary(
            int totalCount,
            int learnedCount,
            int learningCount,
            int mistakeCount,
            int masteredCount = 0,
            int vocabularyMasteredRemainingCount = 0,
            int germanToEnglishCompletionCount = 0,
            int englishToGermanCompletionCount = 0)
        {
            TotalCount = totalCount;
            LearnedCount = learnedCount;
            LearningCount = learningCount;
            MistakeCount = mistakeCount;
            MasteredCount = masteredCount;
            VocabularyMasteredRemainingCount = vocabularyMasteredRemainingCount;
            GermanToEnglishCompletionCount = germanToEnglishCompletionCount;
            EnglishToGermanCompletionCount = englishToGermanCompletionCount;
        }

        public int TotalCount { get; }

        public int LearnedCount { get; }

        public int LearningCount { get; }

        public int MistakeCount { get; }

        public int MasteredCount { get; }

        public int VocabularyMasteredRemainingCount { get; }

        public int GermanToEnglishCompletionCount { get; }

        public int EnglishToGermanCompletionCount { get; }

        public int PartiallyMasteredCount =>
            GermanToEnglishCompletionCount + EnglishToGermanCompletionCount;

        public int RemainingCount => Math.Max(0, TotalCount - LearnedCount);

        public double Progress => TotalCount == 0 ? 0 : (double)LearnedCount / TotalCount;

        public int LearnedPercent => (int)Math.Round(Progress * 100);
    }
}
