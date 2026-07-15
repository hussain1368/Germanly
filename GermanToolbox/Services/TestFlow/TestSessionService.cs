namespace GermanToolbox
{
    public sealed class TestSessionService
    {
        private readonly WordRepository repository;
        private readonly PracticeSettingsService settings;
        private readonly DriveBackupService driveBackupService;
        private readonly Dictionary<PracticeMode, TestSessionResult> lastResults = [];

        public TestSessionService(WordRepository repository, PracticeSettingsService settings, DriveBackupService driveBackupService)
        {
            this.repository = repository;
            this.settings = settings;
            this.driveBackupService = driveBackupService;
        }

        public TestSession? CurrentSession { get; private set; }

        public async Task<TestSession> StartRegularSessionAsync(
            PracticeMode mode,
            VocabularyTestDirection vocabularyDirection = VocabularyTestDirection.GermanToEnglish,
            ArticleCase articleCase = ArticleCase.Nominative,
            ArticleType articleType = ArticleType.Definite,
            IrregularVerbForm irregularVerbForm = IrregularVerbForm.Prateritum,
            IrregularTestMethod irregularTestMethod = IrregularTestMethod.MultipleChoice)
        {
            if (driveBackupService?.IsRestoreInProgress == true)
            {
                throw new InvalidOperationException("A restore is in progress. Please wait until it completes.");
            }

            var words = await repository.GetRegularSessionWordsAsync(
                mode,
                vocabularyDirection,
                irregularVerbForm,
                settings.TestChunkSize,
                settings.LearnedThreshold,
                GetLevel(mode));

            await repository.MarkLearningAsync(mode, words.Select(word => word.Id));

            CurrentSession = new TestSession(
                mode,
                isMistakeReview: false,
                words,
                vocabularyDirection,
                articleCase,
                articleType,
                irregularVerbForm,
                irregularTestMethod);
            return CurrentSession;
        }

        public async Task<TestSession> StartMistakeReviewSessionAsync(
            PracticeMode mode,
            VocabularyTestDirection vocabularyDirection = VocabularyTestDirection.GermanToEnglish,
            ArticleCase articleCase = ArticleCase.Nominative,
            ArticleType articleType = ArticleType.Definite,
            IrregularVerbForm irregularVerbForm = IrregularVerbForm.Prateritum,
            IrregularTestMethod irregularTestMethod = IrregularTestMethod.MultipleChoice)
        {
            if (driveBackupService?.IsRestoreInProgress == true)
            {
                throw new InvalidOperationException("A restore is in progress. Please wait until it completes.");
            }

            var words = await repository.GetMistakeReviewWordsAsync(
                mode,
                irregularVerbForm,
                settings.TestChunkSize,
                GetLevel(mode));
            await repository.MarkLearningAsync(mode, words.Select(word => word.Id));

            CurrentSession = new TestSession(
                mode,
                isMistakeReview: true,
                words,
                vocabularyDirection,
                articleCase,
                articleType,
                irregularVerbForm,
                irregularTestMethod);
            return CurrentSession;
        }

        public TestSession? StartMistakeReviewFromLastResult(PracticeMode mode)
        {
            if (!lastResults.TryGetValue(mode, out var lastResult) || lastResult.MistakenWords.Count == 0)
            {
                return null;
            }

            var shuffledWords = lastResult.MistakenWords
                .OrderBy(_ => Random.Shared.Next())
                .ToList();

            CurrentSession = new TestSession(
                mode,
                isMistakeReview: true,
                shuffledWords,
                lastResult.VocabularyDirection,
                lastResult.ArticleCase,
                lastResult.ArticleType,
                lastResult.IrregularVerbForm,
                lastResult.IrregularTestMethod);
            return CurrentSession;
        }

        public TestSessionResult? GetLastResult(PracticeMode mode) =>
            lastResults.TryGetValue(mode, out var result) ? result : null;

        public Task<PracticeModeSummary> GetPracticeModeSummaryAsync(
            PracticeMode mode,
            VocabularyTestDirection vocabularyDirection = VocabularyTestDirection.GermanToEnglish,
            IrregularVerbForm irregularVerbForm = IrregularVerbForm.Prateritum,
            string level = "") =>
            repository.GetPracticeModeSummaryAsync(
                mode,
                settings.LearnedThreshold,
                vocabularyDirection,
                irregularVerbForm,
                level);

        public Task<IReadOnlyList<LevelWithProgress>> GetLevelsWithProgressAsync() =>
            repository.GetLevelsWithProgressAsync(settings.LearnedThreshold);

        public async Task ResetProgressAsync(IReadOnlyCollection<string> levels)
        {
            await repository.ResetProgressAsync(levels);
            CurrentSession = null;
            lastResults.Clear();
        }

        public TestAnswerResult RecordArticleAnswer(string gender)
        {
            var session = RequireCurrentSession(PracticeMode.Article);
            var currentWord = RequireCurrentWord(session);
            var isCorrect = string.Equals(
                gender,
                currentWord.Word.Gender,
                StringComparison.OrdinalIgnoreCase);

            if (!currentWord.HasScored)
            {
                currentWord.ScoreDelta = isCorrect ? 1 : -1;
                currentWord.WasMistaken = !isCorrect;
                currentWord.HasScored = true;
            }

            if (!isCorrect)
            {
                return new TestAnswerResult(isCorrect: false, shouldAdvance: false, isSessionFinished: false);
            }

            currentWord.IsComplete = true;
            session.MoveNext();

            return new TestAnswerResult(
                isCorrect: true,
                shouldAdvance: true,
                isSessionFinished: session.IsFinished);
        }

        public TestAnswerResult RecordMeaningAnswer(bool isCorrect)
        {
            var session = RequireCurrentSession(PracticeMode.Meaning);
            var currentWord = RequireCurrentWord(session);

            currentWord.ScoreDelta = isCorrect ? 1 : -1;
            currentWord.WasMistaken = !isCorrect;
            currentWord.HasScored = true;
            currentWord.IsComplete = true;

            session.MoveNext();

            return new TestAnswerResult(
                isCorrect,
                shouldAdvance: true,
                isSessionFinished: session.IsFinished);
        }

        public TestAnswerResult RecordIrregularAnswer(
            bool isCorrect,
            bool completeOnIncorrect)
        {
            var session = RequireCurrentSession(PracticeMode.IrregularVerb);
            var currentWord = RequireCurrentWord(session);

            if (!currentWord.HasScored)
            {
                currentWord.ScoreDelta = isCorrect ? 1 : -1;
                currentWord.WasMistaken = !isCorrect;
                currentWord.HasScored = true;
            }

            if (!isCorrect && !completeOnIncorrect)
            {
                return new TestAnswerResult(
                    isCorrect: false,
                    shouldAdvance: false,
                    isSessionFinished: false);
            }

            currentWord.IsComplete = true;
            session.MoveNext();

            return new TestAnswerResult(
                isCorrect,
                shouldAdvance: true,
                isSessionFinished: session.IsFinished);
        }

        public TestAnswerResult RecordPluralAnswer(
            bool isCorrect,
            bool completeOnIncorrect)
        {
            var session = RequireCurrentSession(PracticeMode.Plural);
            var currentWord = RequireCurrentWord(session);

            if (!currentWord.HasScored)
            {
                currentWord.ScoreDelta = isCorrect ? 1 : -1;
                currentWord.WasMistaken = !isCorrect;
                currentWord.HasScored = true;
            }

            if (!isCorrect && !completeOnIncorrect)
            {
                return new TestAnswerResult(
                    isCorrect: false,
                    shouldAdvance: false,
                    isSessionFinished: false);
            }

            currentWord.IsComplete = true;
            session.MoveNext();

            return new TestAnswerResult(
                isCorrect,
                shouldAdvance: true,
                isSessionFinished: session.IsFinished);
        }

        public async Task<TestSessionResult> CompleteCurrentSessionAsync()
        {
            if (CurrentSession is null)
            {
                throw new InvalidOperationException("There is no active test session to complete.");
            }

            var session = CurrentSession;
            var result = CreateResult(session);

            await repository.ApplySessionResultsAsync(
                session.Mode,
                session.VocabularyDirection,
                session.IrregularVerbForm,
                session.Words,
                settings.LearnedThreshold);

            lastResults[session.Mode] = result;
            CurrentSession = null;

            return result;
        }

        public void AbandonCurrentSession(PracticeMode mode)
        {
            if (CurrentSession?.Mode == mode)
            {
                CurrentSession = null;
            }
        }

        private static TestSessionResult CreateResult(TestSession session)
        {
            var completedWords = session.Words.Where(word => word.IsComplete).ToList();
            var mistakenWords = completedWords
                .Where(word => word.WasMistaken)
                .Select(word => word.Word)
                .ToList();

            return new TestSessionResult(
                session.Mode,
                completedWords.Count,
                completedWords.Count(word => !word.WasMistaken),
                mistakenWords.Count,
                DateTimeOffset.Now - session.StartedAt,
                mistakenWords,
                session.VocabularyDirection,
                session.ArticleCase,
                session.ArticleType,
                session.IrregularVerbForm,
                session.IrregularTestMethod);
        }

        private TestSession RequireCurrentSession(PracticeMode mode)
        {
            if (CurrentSession?.Mode != mode)
            {
                throw new InvalidOperationException($"There is no active {mode} test session.");
            }

            return CurrentSession;
        }

        private static TestSessionWord RequireCurrentWord(TestSession session) =>
            session.CurrentWord ?? throw new InvalidOperationException("The current test session is already finished.");

        private string GetLevel(PracticeMode mode) =>
            mode switch
            {
                PracticeMode.Article => settings.ArticleLevel,
                PracticeMode.Plural => settings.PluralLevel,
                PracticeMode.IrregularVerb => settings.IrregularVerbLevel,
                _ => settings.VocabularyLevel
            };
    }
}
