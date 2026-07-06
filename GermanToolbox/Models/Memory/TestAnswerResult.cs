namespace GermanToolbox
{
    public sealed class TestAnswerResult
    {
        public TestAnswerResult(bool isCorrect, bool shouldAdvance, bool isSessionFinished)
        {
            IsCorrect = isCorrect;
            ShouldAdvance = shouldAdvance;
            IsSessionFinished = isSessionFinished;
        }

        public bool IsCorrect { get; }

        public bool ShouldAdvance { get; }

        public bool IsSessionFinished { get; }
    }
}
