using SQLite;

namespace GermanToolbox
{
    public sealed class WordRepository
    {
        public static IReadOnlyList<string> UserProgressColumnOrder { get; } =
        [
            nameof(WordEntry.Id),
            nameof(WordEntry.Learning),
            nameof(WordEntry.ScoreMeaning),
            nameof(WordEntry.ScoreReverseMeaning),
            nameof(WordEntry.ScoreArticle),
            nameof(WordEntry.ScorePlural),
            nameof(WordEntry.ScoreIrregularPrateritum),
            nameof(WordEntry.ScoreIrregularPerfect),
            nameof(WordEntry.MistakeMeaning),
            nameof(WordEntry.MistakeArticle),
            nameof(WordEntry.MistakePlural),
            nameof(WordEntry.MistakeIrregular)
        ];

        private readonly AppDatabase database;
        private readonly WordsDatabaseSeedService wordsDatabaseSeedService;
        private readonly PracticeSettingsService settingsService;
        private readonly SemaphoreSlim initializationLock = new(1, 1);
        private readonly SemaphoreSlim learningSyncLock = new(1, 1);
        private readonly SemaphoreSlim writeLock = new(1, 1);
        private bool isInitialized;
        private int? synchronizedLearningThreshold;

        public WordRepository(
            AppDatabase database,
            WordsDatabaseSeedService wordsDatabaseSeedService,
            PracticeSettingsService settingsService)
        {
            this.database = database;
            this.wordsDatabaseSeedService = wordsDatabaseSeedService;
            this.settingsService = settingsService;
        }

        public async Task<IReadOnlyList<WordEntry>> GetRegularSessionWordsAsync(
            PracticeMode mode,
            VocabularyTestDirection vocabularyDirection,
            IrregularVerbForm irregularVerbForm,
            int chunkSize,
            int learnedThreshold,
            string level)
        {
            await InitializeAsync();
            var words = await WithLearningFlagsAsync(
                learnedThreshold,
                () => GetSessionWordsAsync(
                    mode,
                    vocabularyDirection,
                    irregularVerbForm,
                    chunkSize,
                    learnedThreshold,
                    level));
            return words.OrderBy(_ => Random.Shared.Next()).ToList();
        }

        public async Task<IReadOnlyList<WordEntry>> GetMistakeReviewWordsAsync(
            PracticeMode mode,
            IrregularVerbForm irregularVerbForm,
            int chunkSize,
            string level)
        {
            await InitializeAsync();

            var mistakeColumn = GetMistakeColumn(mode);
            var sql = $$"""
                SELECT *
                FROM Words
                WHERE {{GetLevelFilterWhereClause(level)}}
                  AND {{GetEligibilityWhereClause(mode)}}
                  AND {{mistakeColumn}} = 1
                ORDER BY RANDOM()
                LIMIT ?
                """;

            var parameters = new List<object>();
            AddLevelFilterParameter(parameters, level);
            parameters.Add(chunkSize);

            return await database.Connection.QueryAsync<WordEntry>(sql, parameters.ToArray());
        }

        public async Task<IReadOnlyList<WordEntry>> SearchWordsAsync(
            string query,
            int maximumCount = 25)
        {
            await InitializeAsync();

            var normalizedQuery = query.Trim();
            if (normalizedQuery.Length < 2 || maximumCount <= 0)
            {
                return [];
            }

            var searchVariants = new[]
                {
                    normalizedQuery,
                    normalizedQuery.ToLowerInvariant(),
                    normalizedQuery.ToUpperInvariant()
                }
                .Distinct(StringComparer.Ordinal)
                .Select(EscapeLikePattern)
                .ToList();
            var whereClause = string.Join(
                " OR ",
                searchVariants.Select(_ =>
                    $"({nameof(WordEntry.Word)} LIKE ? ESCAPE '\\' " +
                    $"OR {nameof(WordEntry.Translation)} LIKE ? ESCAPE '\\')"));
            var wordPrefixClause = string.Join(
                " OR ",
                searchVariants.Select(_ =>
                    $"{nameof(WordEntry.Word)} LIKE ? ESCAPE '\\'"));
            var translationPrefixClause = string.Join(
                " OR ",
                searchVariants.Select(_ =>
                    $"{nameof(WordEntry.Translation)} LIKE ? ESCAPE '\\'"));
            var sql = $$"""
                SELECT *
                FROM Words
                WHERE {{whereClause}}
                ORDER BY
                    CASE
                        WHEN {{wordPrefixClause}} THEN 0
                        WHEN {{translationPrefixClause}} THEN 1
                        ELSE 2
                    END,
                    LENGTH({{nameof(WordEntry.Word)}}),
                    {{nameof(WordEntry.Word)}} COLLATE NOCASE
                LIMIT ?
                """;

            var parameters = new List<object>();
            foreach (var variant in searchVariants)
            {
                var containsPattern = $"%{variant}%";
                parameters.Add(containsPattern);
                parameters.Add(containsPattern);
            }

            parameters.AddRange(searchVariants.Select(variant => $"{variant}%"));
            parameters.AddRange(searchVariants.Select(variant => $"{variant}%"));
            parameters.Add(maximumCount);

            return await database.Connection.QueryAsync<WordEntry>(
                sql,
                parameters.ToArray());
        }

        public async Task<WordEntry?> GetWordByIdAsync(int wordId)
        {
            await InitializeAsync();
            return await database.Connection.FindAsync<WordEntry>(wordId);
        }

        public async Task<WordEntry?> GetWordOfTheDayAsync(DateOnly date)
        {
            await InitializeAsync();

            var totalCount = await CountAsync("SELECT COUNT(*) FROM Words");
            if (totalCount == 0)
            {
                return null;
            }

            var seed = unchecked((uint)date.DayNumber);
            seed ^= seed >> 16;
            seed *= 0x7FEB352D;
            seed ^= seed >> 15;
            seed *= 0x846CA68B;
            seed ^= seed >> 16;

            var offset = (int)(seed % (uint)totalCount);
            var words = await database.Connection.QueryAsync<WordEntry>(
                "SELECT * FROM Words ORDER BY Id LIMIT 1 OFFSET ?",
                offset);
            return words.FirstOrDefault();
        }

        public async Task<(int MasteredCount, int TotalCount)> GetOverallMasteryAsync(
            int learnedThreshold)
        {
            await InitializeAsync();

            var rows = await database.Connection.QueryAsync<OverallMasteryCounts>(
                $$"""
                SELECT
                    COUNT(*) AS TotalCount,
                    COALESCE(
                        SUM(CASE WHEN {{GetCompletedWhereClause()}} THEN 1 ELSE 0 END),
                        0) AS MasteredCount
                FROM Words
                """,
                learnedThreshold,
                learnedThreshold,
                learnedThreshold,
                learnedThreshold,
                learnedThreshold,
                learnedThreshold);
            var counts = rows.FirstOrDefault() ?? new OverallMasteryCounts();

            return (counts.MasteredCount, counts.TotalCount);
        }

        public async Task<IReadOnlyList<LevelMasterySummary>> GetLevelMasterySummariesAsync(
            int learnedThreshold)
        {
            await InitializeAsync();

            var completedWhereClause = GetCompletedWhereClause();
            var rows = await WithLearningFlagsAsync(
                learnedThreshold,
                () => database.Connection.QueryAsync<LevelMasteryCounts>(
                    $$"""
                    WITH LevelRows AS (
                        SELECT
                            {{nameof(WordEntry.Level)}} AS Level,
                            CASE WHEN {{completedWhereClause}} THEN 1 ELSE 0 END AS IsMastered,
                            CASE WHEN {{nameof(WordEntry.Learning)}} = 1 THEN 1 ELSE 0 END AS IsLearning
                        FROM Words
                    )
                    SELECT
                        Level,
                        COALESCE(SUM(IsMastered), 0) AS MasteredCount,
                        COALESCE(
                            SUM(
                                CASE WHEN IsLearning = 1 OR IsMastered = 1
                                THEN 1 ELSE 0 END),
                            0) AS ActiveCount
                    FROM LevelRows
                    GROUP BY Level
                    HAVING ActiveCount > 0
                    """,
                    learnedThreshold,
                    learnedThreshold,
                    learnedThreshold,
                    learnedThreshold,
                    learnedThreshold,
                    learnedThreshold));

            return rows
                .Where(row => !string.IsNullOrWhiteSpace(row.Level))
                .OrderBy(row => GetLevelSortKey(row.Level))
                .ThenBy(row => row.Level, StringComparer.OrdinalIgnoreCase)
                .Select(row => new LevelMasterySummary(
                    row.Level,
                    row.MasteredCount,
                    row.ActiveCount))
                .ToList();
        }

        public async Task<PracticeModeSummary> GetPracticeModeSummaryAsync(
            PracticeMode mode,
            int learnedThreshold,
            VocabularyTestDirection vocabularyDirection = VocabularyTestDirection.GermanToEnglish,
            IrregularVerbForm irregularVerbForm = IrregularVerbForm.Prateritum,
            string level = "")
        {
            await InitializeAsync();

            var scoreColumn = GetScoreColumn(mode, vocabularyDirection, irregularVerbForm);
            var mistakeColumn = GetMistakeColumn(mode);
            var whereClause = GetEligibilityWhereClause(mode);

            var (
                learnedCondition,
                learningCondition,
                masteredCondition,
                vocabularyMasteredRemainingCondition,
                germanToEnglishCompletionCondition,
                englishToGermanCompletionCondition,
                parameters) =
                mode switch
                {
                    PracticeMode.Meaning => (
                        $"{nameof(WordEntry.ScoreMeaning)} >= ? " +
                        $"OR {nameof(WordEntry.ScoreReverseMeaning)} >= ?",
                        $"{nameof(WordEntry.Learning)} = 1 AND {scoreColumn} < ?",
                        $"{nameof(WordEntry.ScoreMeaning)} >= ? " +
                        $"AND {nameof(WordEntry.ScoreReverseMeaning)} >= ?",
                        "0 = 1",
                        $"{nameof(WordEntry.ScoreReverseMeaning)} >= ? " +
                        $"AND {nameof(WordEntry.ScoreMeaning)} < ?",
                        $"{nameof(WordEntry.ScoreMeaning)} >= ? " +
                        $"AND {nameof(WordEntry.ScoreReverseMeaning)} < ?",
                        new object[]
                        {
                            learnedThreshold,
                            learnedThreshold,
                            learnedThreshold,
                            learnedThreshold,
                            learnedThreshold,
                            learnedThreshold,
                            learnedThreshold,
                            learnedThreshold,
                            learnedThreshold
                        }),
                    PracticeMode.IrregularVerb => (
                        GetIrregularScoresCompletedWhereClause(),
                        $"{nameof(WordEntry.Learning)} = 1 " +
                        $"AND NOT ({GetIrregularScoresCompletedWhereClause()})",
                        "0 = 1",
                        $"({GetVocabularyMasteredWhereClause()}) AND {scoreColumn} < ?",
                        "0 = 1",
                        "0 = 1",
                        new object[]
                        {
                            learnedThreshold,
                            learnedThreshold,
                            learnedThreshold,
                            learnedThreshold,
                            learnedThreshold,
                            learnedThreshold,
                            learnedThreshold
                        }),
                    _ => (
                        $"{scoreColumn} >= ?",
                        $"{nameof(WordEntry.Learning)} = 1 AND {scoreColumn} < ?",
                        "0 = 1",
                        $"({GetVocabularyMasteredWhereClause()}) AND {scoreColumn} < ?",
                        "0 = 1",
                        "0 = 1",
                        new object[]
                        {
                            learnedThreshold,
                            learnedThreshold,
                            learnedThreshold,
                            learnedThreshold,
                            learnedThreshold
                        })
                };
            var queryParameters = parameters.ToList();
            AddLevelFilterParameter(queryParameters, level);

            var rows = await WithLearningFlagsAsync(
                learnedThreshold,
                () => database.Connection.QueryAsync<PracticeSummaryCounts>(
                    $$"""
                    SELECT
                        COUNT(*) AS TotalCount,
                        COALESCE(
                            SUM(CASE WHEN {{learnedCondition}} THEN 1 ELSE 0 END),
                            0) AS LearnedCount,
                        COALESCE(
                            SUM(CASE WHEN {{learningCondition}} THEN 1 ELSE 0 END),
                            0) AS LearningCount,
                        COALESCE(
                            SUM(CASE WHEN {{mistakeColumn}} = 1 THEN 1 ELSE 0 END),
                            0) AS MistakeCount,
                        COALESCE(
                            SUM(CASE WHEN {{masteredCondition}} THEN 1 ELSE 0 END),
                            0) AS MasteredCount,
                        COALESCE(
                            SUM(
                                CASE WHEN {{vocabularyMasteredRemainingCondition}}
                                THEN 1 ELSE 0 END),
                            0) AS VocabularyMasteredRemainingCount,
                        COALESCE(
                            SUM(
                                CASE WHEN {{germanToEnglishCompletionCondition}}
                                THEN 1 ELSE 0 END),
                            0) AS GermanToEnglishCompletionCount,
                        COALESCE(
                            SUM(
                                CASE WHEN {{englishToGermanCompletionCondition}}
                                THEN 1 ELSE 0 END),
                            0) AS EnglishToGermanCompletionCount
                    FROM Words
                    WHERE {{whereClause}}
                      AND {{GetLevelFilterWhereClause(level)}}
                    """,
                    queryParameters.ToArray()));
            var counts = rows.FirstOrDefault() ?? new PracticeSummaryCounts();

            return new PracticeModeSummary(
                counts.TotalCount,
                counts.LearnedCount,
                counts.LearningCount,
                counts.MistakeCount,
                counts.MasteredCount,
                counts.VocabularyMasteredRemainingCount,
                counts.GermanToEnglishCompletionCount,
                counts.EnglishToGermanCompletionCount);
        }

        public async Task MarkLearningAsync(PracticeMode mode, IEnumerable<int> wordIds)
        {
            await InitializeAsync();

            var ids = wordIds.Distinct().ToList();
            if (ids.Count == 0)
            {
                return;
            }

            var placeholders = string.Join(",", ids.Select(_ => "?"));
            var sql = $"UPDATE Words SET {nameof(WordEntry.Learning)} = 1 WHERE Id IN ({placeholders})";
            await ExecuteWriteAsync(() => database.Connection.ExecuteAsync(sql, ids.Cast<object>().ToArray()));
            settingsService.MarkBackupNeeded();
        }

        public async Task ApplySessionResultsAsync(
            PracticeMode mode,
            VocabularyTestDirection vocabularyDirection,
            IrregularVerbForm irregularVerbForm,
            IEnumerable<TestSessionWord> sessionWords,
            int learnedThreshold)
        {
            await InitializeAsync();

            var wordsToUpdate = sessionWords.Select(sessionWord =>
            {
                ApplySessionWordResult(
                    mode,
                    vocabularyDirection,
                    irregularVerbForm,
                    sessionWord,
                    learnedThreshold);
                return sessionWord.Word;
            }).ToList();

            if (wordsToUpdate.Count > 0)
            {
                await ExecuteLearningFlagWriteAsync(
                    learnedThreshold,
                    () => database.Connection.UpdateAllAsync(wordsToUpdate));
                settingsService.MarkBackupNeeded();
            }
        }

        public async Task ResetProgressAsync()
        {
            await InitializeAsync();

            await ExecuteWriteAsync(async () =>
            {
                var words = await database.Connection.Table<WordEntry>().ToListAsync();
                foreach (var word in words)
                {
                    word.ScoreMeaning = 0;
                    word.ScoreReverseMeaning = 0;
                    word.ScoreArticle = 0;
                    word.ScoreIrregularPrateritum = 0;
                    word.ScoreIrregularPerfect = 0;
                    word.ScorePlural = 0;
                    word.MistakeArticle = false;
                    word.MistakeMeaning = false;
                    word.MistakeIrregular = false;
                    word.MistakePlural = false;
                    word.Learning = false;
                }

                if (words.Count > 0)
                {
                    await database.Connection.UpdateAllAsync(words);
                }
            });
            settingsService.MarkBackupNeeded();
        }

        public sealed class WordProgressRow
        {
            public int Id { get; set; }
            public bool Learning { get; set; }
            public int ScoreMeaning { get; set; }
            public int ScoreReverseMeaning { get; set; }
            public int ScoreArticle { get; set; }
            public int ScorePlural { get; set; }
            public int ScoreIrregularPrateritum { get; set; }
            public int ScoreIrregularPerfect { get; set; }
            public bool MistakeMeaning { get; set; }
            public bool MistakeArticle { get; set; }
            public bool MistakePlural { get; set; }
            public bool MistakeIrregular { get; set; }
        }

        public async Task<List<WordProgressRow>> GetUserProgressRowsAsync()
        {
            await InitializeAsync();

            var selectedColumns = string.Join(", ", UserProgressColumnOrder.Select(QuoteIdentifier));
            var rows = await database.Connection.QueryAsync<WordProgressRow>(
                $"SELECT {selectedColumns} FROM Words");

            return rows.ToList();
        }

        public async Task ApplyUserProgressAsync(
            IEnumerable<WordProgressRow> rows,
            IReadOnlyCollection<string> columnsToApply)
        {
            await InitializeAsync();
            var list = rows.ToList();
            if (list.Count == 0)
            {
                return;
            }

            var allowedColumns = UserProgressColumnOrder
                .Skip(1)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var updateColumns = columnsToApply
                .Where(allowedColumns.Contains)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (updateColumns.Count == 0)
            {
                return;
            }

            var setClause = string.Join(
                ", ",
                updateColumns.Select(column => $"{QuoteIdentifier(column)} = ?"));
            var sql = $"UPDATE Words SET {setClause} WHERE {QuoteIdentifier(nameof(WordEntry.Id))} = ?";

            await ExecuteWriteAsync(() => database.Connection.RunInTransactionAsync(conn =>
            {
                foreach (var w in list)
                {
                    var parameters = updateColumns
                        .Select(column => GetProgressColumnValue(w, column))
                        .Append(w.Id)
                        .ToArray();
                    conn.Execute(sql, parameters);
                }
            }));
            settingsService.MarkBackupNeeded();
        }

        private static object GetProgressColumnValue(WordProgressRow row, string column) =>
            column switch
            {
                nameof(WordEntry.Learning) => row.Learning ? 1 : 0,
                nameof(WordEntry.ScoreMeaning) => row.ScoreMeaning,
                nameof(WordEntry.ScoreReverseMeaning) => row.ScoreReverseMeaning,
                nameof(WordEntry.ScoreArticle) => row.ScoreArticle,
                nameof(WordEntry.ScorePlural) => row.ScorePlural,
                nameof(WordEntry.ScoreIrregularPrateritum) => row.ScoreIrregularPrateritum,
                nameof(WordEntry.ScoreIrregularPerfect) => row.ScoreIrregularPerfect,
                nameof(WordEntry.MistakeMeaning) => row.MistakeMeaning ? 1 : 0,
                nameof(WordEntry.MistakeArticle) => row.MistakeArticle ? 1 : 0,
                nameof(WordEntry.MistakePlural) => row.MistakePlural ? 1 : 0,
                nameof(WordEntry.MistakeIrregular) => row.MistakeIrregular ? 1 : 0,
                _ => throw new ArgumentOutOfRangeException(nameof(column), column, "Unknown progress column.")
            };

        private static string QuoteIdentifier(string identifier) =>
            $"\"{identifier.Replace("\"", "\"\"")}\"";

        private async Task InitializeAsync()
        {
            if (isInitialized)
            {
                return;
            }

            await initializationLock.WaitAsync();
            try
            {
                if (isInitialized)
                {
                    return;
                }

                await database.Connection.CreateTableAsync<WordEntry>();
                await wordsDatabaseSeedService.SeedOnceAsync();

                isInitialized = true;
            }
            finally
            {
                initializationLock.Release();
            }
        }

        private Task<int> CountAsync(string sql, params object[] args) =>
            database.Connection.ExecuteScalarAsync<int>(sql, args);

        private static string EscapeLikePattern(string value) =>
            value
                .Replace(@"\", @"\\", StringComparison.Ordinal)
                .Replace("%", @"\%", StringComparison.Ordinal)
                .Replace("_", @"\_", StringComparison.Ordinal);

        private async Task ExecuteWriteAsync(Func<Task> writeAction)
        {
            await writeLock.WaitAsync();
            try
            {
                await writeAction();
            }
            finally
            {
                writeLock.Release();
            }
        }

        private async Task<T> WithLearningFlagsAsync<T>(
            int learnedThreshold,
            Func<Task<T>> action)
        {
            await learningSyncLock.WaitAsync();
            try
            {
                if (synchronizedLearningThreshold != learnedThreshold)
                {
                    await SyncLearningFlagsAsync(learnedThreshold);
                    synchronizedLearningThreshold = learnedThreshold;
                }

                return await action();
            }
            finally
            {
                learningSyncLock.Release();
            }
        }

        private async Task ExecuteLearningFlagWriteAsync(
            int learnedThreshold,
            Func<Task> writeAction)
        {
            await learningSyncLock.WaitAsync();
            try
            {
                await ExecuteWriteAsync(writeAction);

                if (synchronizedLearningThreshold != learnedThreshold)
                {
                    synchronizedLearningThreshold = null;
                }
            }
            finally
            {
                learningSyncLock.Release();
            }
        }

        private async Task SyncLearningFlagsAsync(int learnedThreshold)
        {
            var completedWhereClause = GetCompletedWhereClause();
            var clearCompletedSql = $$"""
                UPDATE Words
                SET {{nameof(WordEntry.Learning)}} = 0
                WHERE {{nameof(WordEntry.Learning)}} = 1
                  AND {{completedWhereClause}}
                """;
            var promoteInProgressSql = $$"""
                UPDATE Words
                SET {{nameof(WordEntry.Learning)}} = 1
                WHERE {{nameof(WordEntry.Learning)}} = 0
                  AND NOT ({{completedWhereClause}})
                  AND (
                    {{nameof(WordEntry.ScoreMeaning)}} <> 0
                    OR {{nameof(WordEntry.ScoreReverseMeaning)}} <> 0
                    OR {{nameof(WordEntry.ScoreArticle)}} <> 0
                    OR {{nameof(WordEntry.ScoreIrregularPrateritum)}} <> 0
                    OR {{nameof(WordEntry.ScoreIrregularPerfect)}} <> 0
                    OR {{nameof(WordEntry.ScorePlural)}} <> 0
                    OR {{nameof(WordEntry.MistakeMeaning)}} = 1
                    OR {{nameof(WordEntry.MistakeArticle)}} = 1
                    OR {{nameof(WordEntry.MistakeIrregular)}} = 1
                    OR {{nameof(WordEntry.MistakePlural)}} = 1
                  )
                """;

            await ExecuteWriteAsync(async () =>
            {
                await database.Connection.ExecuteAsync(
                    clearCompletedSql,
                    learnedThreshold,
                    learnedThreshold,
                    learnedThreshold,
                    learnedThreshold,
                    learnedThreshold,
                    learnedThreshold);
                await database.Connection.ExecuteAsync(
                    promoteInProgressSql,
                    learnedThreshold,
                    learnedThreshold,
                    learnedThreshold,
                    learnedThreshold,
                    learnedThreshold,
                    learnedThreshold);
            });
        }

        public sealed class PracticeSummaryCounts
        {
            public int TotalCount { get; set; }

            public int LearnedCount { get; set; }

            public int LearningCount { get; set; }

            public int MistakeCount { get; set; }

            public int MasteredCount { get; set; }

            public int VocabularyMasteredRemainingCount { get; set; }

            public int GermanToEnglishCompletionCount { get; set; }

            public int EnglishToGermanCompletionCount { get; set; }
        }

        public sealed class OverallMasteryCounts
        {
            public int TotalCount { get; set; }

            public int MasteredCount { get; set; }
        }

        public sealed class LevelMasteryCounts
        {
            public string Level { get; set; } = string.Empty;

            public int MasteredCount { get; set; }

            public int ActiveCount { get; set; }
        }

        private async Task<List<WordEntry>> GetSessionWordsAsync(
            PracticeMode mode,
            VocabularyTestDirection vocabularyDirection,
            IrregularVerbForm irregularVerbForm,
            int limit,
            int learnedThreshold,
            string level)
        {
            var scoreColumn = GetScoreColumn(mode, vocabularyDirection, irregularVerbForm);
            var learningPriorityColumn =
                GetLearningPriorityColumn(mode, vocabularyDirection);
            var learningPriority = learningPriorityColumn is not null
                ? $", CASE WHEN {nameof(WordEntry.Learning)} = 1 " +
                  $"THEN {learningPriorityColumn} END DESC"
                : string.Empty;

            var sql = $$"""
                SELECT *
                FROM Words
                WHERE {{GetLevelFilterWhereClause(level)}}
                  AND {{GetEligibilityWhereClause(mode)}}
                  AND {{scoreColumn}} < ?
                ORDER BY
                    {{nameof(WordEntry.Learning)}} DESC
                    {{learningPriority}},
                    RANDOM()
                LIMIT ?
                """;

            var parameters = new List<object>();
            AddLevelFilterParameter(parameters, level);
            parameters.Add(learnedThreshold);
            parameters.Add(limit);

            return await database.Connection.QueryAsync<WordEntry>(sql, parameters.ToArray());
        }

        private async Task<List<WordEntry>> GetSessionWordsAsync456(
            PracticeMode mode,
            VocabularyTestDirection vocabularyDirection,
            IrregularVerbForm irregularVerbForm,
            int limit,
            int learnedThreshold,
            string level)
        {
            var sql = $"select * from Words where id in (1228, 922, 1237, 1412, 692, 324, 1379)";

            return await database.Connection.QueryAsync<WordEntry>(sql);
        }

        private static void ApplySessionWordResult(
            PracticeMode mode,
            VocabularyTestDirection vocabularyDirection,
            IrregularVerbForm irregularVerbForm,
            TestSessionWord sessionWord,
            int learnedThreshold)
        {
            var word = sessionWord.Word;

            if (mode == PracticeMode.Article)
            {
                word.ScoreArticle += sessionWord.ScoreDelta;
                word.MistakeArticle = sessionWord.WasMistaken;
                word.Learning = !IsFullyLearned(word, learnedThreshold);

                return;
            }

            if (mode == PracticeMode.IrregularVerb)
            {
                if (irregularVerbForm == IrregularVerbForm.Perfect)
                {
                    word.ScoreIrregularPerfect += sessionWord.ScoreDelta;
                }
                else
                {
                    word.ScoreIrregularPrateritum += sessionWord.ScoreDelta;
                }

                word.MistakeIrregular = sessionWord.WasMistaken;
                word.Learning = !IsFullyLearned(word, learnedThreshold);

                return;
            }

            if (mode == PracticeMode.Plural)
            {
                word.ScorePlural += sessionWord.ScoreDelta;
                word.MistakePlural = sessionWord.WasMistaken;
                word.Learning = !IsFullyLearned(word, learnedThreshold);

                return;
            }

            if (vocabularyDirection == VocabularyTestDirection.EnglishToGerman)
            {
                word.ScoreReverseMeaning += sessionWord.ScoreDelta;
            }
            else
            {
                word.ScoreMeaning += sessionWord.ScoreDelta;
            }

            word.MistakeMeaning = sessionWord.WasMistaken;
            word.Learning = !IsFullyLearned(word, learnedThreshold);
        }

        private static string GetEligibilityWhereClause(PracticeMode mode) =>
            mode switch
            {
                PracticeMode.Article => "Type = 'noun' AND Gender IN ('m', 'f', 'n')",
                PracticeMode.Plural =>
                    $"Type = 'noun' " +
                    $"AND {nameof(WordEntry.Plural)} IS NOT NULL " +
                    $"AND TRIM({nameof(WordEntry.Plural)}) <> ''",
                PracticeMode.IrregularVerb =>
                    $"{nameof(WordEntry.IsStrong)} = 1 " +
                    $"AND {nameof(WordEntry.Past)} IS NOT NULL " +
                    $"AND {nameof(WordEntry.Perfekt)} IS NOT NULL",
                _ => "1 = 1"
            };

        private static string GetScoreColumn(
            PracticeMode mode,
            VocabularyTestDirection vocabularyDirection,
            IrregularVerbForm irregularVerbForm) =>
            mode switch
            {
                PracticeMode.Article => nameof(WordEntry.ScoreArticle),
                PracticeMode.Plural => nameof(WordEntry.ScorePlural),
                PracticeMode.IrregularVerb when irregularVerbForm == IrregularVerbForm.Prateritum =>
                    nameof(WordEntry.ScoreIrregularPrateritum),
                PracticeMode.IrregularVerb when irregularVerbForm == IrregularVerbForm.Perfect =>
                    nameof(WordEntry.ScoreIrregularPerfect),
                _ when vocabularyDirection == VocabularyTestDirection.EnglishToGerman =>
                    nameof(WordEntry.ScoreReverseMeaning),
                _ => nameof(WordEntry.ScoreMeaning)
            };

        private static string GetMistakeColumn(PracticeMode mode) =>
            mode switch
            {
                PracticeMode.Article => nameof(WordEntry.MistakeArticle),
                PracticeMode.Plural => nameof(WordEntry.MistakePlural),
                PracticeMode.IrregularVerb => nameof(WordEntry.MistakeIrregular),
                _ => nameof(WordEntry.MistakeMeaning)
            };

        private static string? GetLearningPriorityColumn(
            PracticeMode mode,
            VocabularyTestDirection vocabularyDirection) =>
            mode switch
            {
                PracticeMode.Article or PracticeMode.Plural or PracticeMode.IrregularVerb =>
                    nameof(WordEntry.ScoreMeaning),
                PracticeMode.Meaning
                    when vocabularyDirection == VocabularyTestDirection.EnglishToGerman =>
                    nameof(WordEntry.ScoreMeaning),
                PracticeMode.Meaning => nameof(WordEntry.ScoreReverseMeaning),
                _ => null
            };

        private static bool IsFullyLearned(WordEntry word, int learnedThreshold) =>
            word.ScoreMeaning >= learnedThreshold &&
            word.ScoreReverseMeaning >= learnedThreshold &&
            (!IsArticleEligible(word) || word.ScoreArticle >= learnedThreshold) &&
            (!IsPluralEligible(word) || word.ScorePlural >= learnedThreshold) &&
            (!word.IsStrong ||
                (word.ScoreIrregularPrateritum >= learnedThreshold &&
                 word.ScoreIrregularPerfect >= learnedThreshold));

        private static bool IsArticleEligible(WordEntry word) =>
            string.Equals(word.Type, "noun", StringComparison.OrdinalIgnoreCase) &&
            (word.Gender == "m" || word.Gender == "f" || word.Gender == "n");

        private static bool IsPluralEligible(WordEntry word) =>
            string.Equals(word.Type, "noun", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(word.Plural);

        private static int GetLevelSortKey(string level) =>
            level.Trim().ToUpperInvariant() switch
            {
                "A1" => 0,
                "A2" => 1,
                "B1" => 2,
                "B2" => 3,
                "C1" => 4,
                "C2" => 5,
                _ => 100
            };

        private static string GetLevelFilterWhereClause(string level) =>
            string.IsNullOrWhiteSpace(level)
                ? "1 = 1"
                : $"{nameof(WordEntry.Level)} = ?";

        private static void AddLevelFilterParameter(List<object> parameters, string level)
        {
            if (!string.IsNullOrWhiteSpace(level))
            {
                parameters.Add(level.Trim().ToUpperInvariant());
            }
        }

        private static string GetIrregularScoresCompletedWhereClause() =>
            $$"""
            {{nameof(WordEntry.ScoreIrregularPrateritum)}} >= ?
            AND {{nameof(WordEntry.ScoreIrregularPerfect)}} >= ?
            """;

        private static string GetVocabularyMasteredWhereClause() =>
            $$"""
            {{nameof(WordEntry.ScoreMeaning)}} >= ?
            AND {{nameof(WordEntry.ScoreReverseMeaning)}} >= ?
            """;

        private static string GetCompletedWhereClause() =>
            $$"""
            {{nameof(WordEntry.ScoreMeaning)}} >= ?
            AND {{nameof(WordEntry.ScoreReverseMeaning)}} >= ?
            AND (
                Type <> 'noun'
                OR Gender IS NULL
                OR Gender NOT IN ('m', 'f', 'n')
                OR {{nameof(WordEntry.ScoreArticle)}} >= ?
            )
            AND (
                Type <> 'noun'
                OR {{nameof(WordEntry.Plural)}} IS NULL
                OR TRIM({{nameof(WordEntry.Plural)}}) = ''
                OR {{nameof(WordEntry.ScorePlural)}} >= ?
            )
            AND (
                {{nameof(WordEntry.IsStrong)}} = 0
                OR ({{GetIrregularScoresCompletedWhereClause()}})
            )
            """;
    }
}