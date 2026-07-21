using Microsoft.Maui.Storage;
using SQLite;

namespace GermanToolbox
{
    public sealed class WordsDatabaseSeedService
    {
        private const string SeedDatabaseFileName = "seeder.db3";
        private const string SeedPayloadAssetName = "seed/seeder.payload";
        private const string SeedCacheDirectoryPrefix = "seeder-";
        private const string SeedKey = "WordsDatabaseV14";
        private static readonly string[] WordColumnOrder =
        [
            nameof(WordEntry.Id),
            nameof(WordEntry.Word),
            nameof(WordEntry.Translation),
            nameof(WordEntry.Type),
            nameof(WordEntry.Gender),
            nameof(WordEntry.GenderHint),
            nameof(WordEntry.Plural),
            nameof(WordEntry.Level),
            nameof(WordEntry.IsStrong),
            nameof(WordEntry.IsSeparable),
            nameof(WordEntry.Prefix),
            nameof(WordEntry.Past),
            nameof(WordEntry.Perfekt),
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
        private static readonly string[] HintColumnOrder =
        [
            nameof(HintEntry.Id),
            nameof(HintEntry.Gender),
            nameof(HintEntry.Rule),
            nameof(HintEntry.Suffix),
            nameof(HintEntry.Percentage)
        ];

        private readonly AppDatabase database;

        public WordsDatabaseSeedService(AppDatabase database)
        {
            this.database = database;
        }

        public async Task SeedOnceAsync()
        {
            await database.Connection.CreateTableAsync<SeedRun>();
            await EnsureCurrentWordSchemaAsync();
            await EnsureCurrentHintSchemaAsync();

            if (await database.Connection.FindAsync<SeedRun>(SeedKey) is not null)
            {
                return;
            }

            var seedData = await LoadSeedDataAsync();
            if (seedData.Words.Count == 0)
            {
                throw new InvalidDataException(
                    $"{SeedDatabaseFileName} did not contain any vocabulary rows.");
            }

            var duplicateId = seedData.Words
                .GroupBy(word => word.Id)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicateId is not null)
            {
                throw new InvalidDataException(
                    $"{SeedDatabaseFileName} contains duplicate ID {duplicateId.Key}.");
            }

            await database.Connection.RunInTransactionAsync(connection =>
            {
                connection.Execute("DELETE FROM \"Words\"");
                connection.InsertAll(seedData.Words, runInTransaction: false);
                connection.Execute("DELETE FROM \"Hints\"");
                connection.InsertAll(seedData.Hints, runInTransaction: false);
                connection.Insert(
                    new SeedRun
                    {
                        Key = SeedKey,
                        RanAtUtc = DateTime.UtcNow
                    });
            });
        }

        private static async Task<SeedData> LoadSeedDataAsync()
        {
            DeleteStaleSeedCacheDirectories();

            var temporaryDirectory = Path.Combine(
                FileSystem.CacheDirectory,
                $"{SeedCacheDirectoryPrefix}{Guid.NewGuid():N}");
            var temporaryPath = Path.Combine(temporaryDirectory, SeedDatabaseFileName);

            try
            {
                Directory.CreateDirectory(temporaryDirectory);

                using (var packageStream =
                    await FileSystem.OpenAppPackageFileAsync(SeedPayloadAssetName))
                await using (var temporaryStream = new FileStream(
                    temporaryPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    FileOptions.Asynchronous))
                {
                    await packageStream.CopyToAsync(temporaryStream);
                }

                return await Task.Run(() => LoadValidatedSeedData(temporaryPath));
            }
            finally
            {
                if (Directory.Exists(temporaryDirectory))
                {
                    Directory.Delete(temporaryDirectory, recursive: true);
                }
            }
        }

        private static void DeleteStaleSeedCacheDirectories()
        {
            if (!Directory.Exists(FileSystem.CacheDirectory))
            {
                return;
            }

            foreach (var directory in Directory.EnumerateDirectories(
                FileSystem.CacheDirectory,
                $"{SeedCacheDirectoryPrefix}*"))
            {
                try
                {
                    Directory.Delete(directory, recursive: true);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        private static SeedData LoadValidatedSeedData(string databasePath)
        {
            using var sourceConnection = new SQLiteConnection(
                databasePath,
                SQLiteOpenFlags.ReadOnly);
            ValidateSourceDatabase(sourceConnection);
            return new SeedData(
                sourceConnection.Table<WordEntry>().ToList(),
                sourceConnection.Table<HintEntry>().ToList());
        }

        private static void ValidateSourceDatabase(SQLiteConnection sourceConnection)
        {
            var integrityResult = sourceConnection.ExecuteScalar<string>(
                "PRAGMA integrity_check");
            if (!string.Equals(integrityResult, "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"{SeedDatabaseFileName} failed SQLite integrity validation: {integrityResult}");
            }

            ValidateTableSchema(sourceConnection, "Words", WordColumnOrder);
            ValidateTableSchema(sourceConnection, "Hints", HintColumnOrder);
        }

        private static void ValidateTableSchema(
            SQLiteConnection sourceConnection,
            string tableName,
            IReadOnlyList<string> expectedColumnOrder)
        {
            var sourceColumns = sourceConnection
                .Query<SchemaColumn>($"PRAGMA table_info(\"{tableName}\")")
                .Select(column => column.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var expectedColumns = expectedColumnOrder
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missingRequiredColumns = expectedColumnOrder
                .Where(column =>
                    !IsDefaultableSourceColumn(column) &&
                    !sourceColumns.Contains(column))
                .ToList();
            var unexpectedColumns = sourceColumns
                .Where(column => !expectedColumns.Contains(column))
                .ToList();

            if (missingRequiredColumns.Count > 0 || unexpectedColumns.Count > 0)
            {
                throw new InvalidDataException(
                    $"{SeedDatabaseFileName} does not contain the expected {tableName} schema.");
            }
        }

        private async Task EnsureCurrentWordSchemaAsync()
        {
            var columns = await database.Connection.QueryAsync<SchemaColumn>(
                "PRAGMA table_info(\"Words\")");
            if (columns.Count == 0)
            {
                await database.Connection.CreateTableAsync<WordEntry>();
                return;
            }

            var currentOrder = columns.Select(column => column.Name).ToList();
            if (currentOrder.SequenceEqual(
                WordColumnOrder,
                StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            var sourceColumns = currentOrder.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var hasLegacyIrregularScore = sourceColumns.Contains("ScoreIrregular");
            var targetColumns = string.Join(
                ", ",
                WordColumnOrder.Select(QuoteIdentifier));
            var sourceExpressions = string.Join(
                ", ",
                WordColumnOrder.Select(column =>
                    GetMigrationExpression(
                        column,
                        sourceColumns,
                        hasLegacyIrregularScore)));

            await database.Connection.RunInTransactionAsync(connection =>
            {
                connection.Execute("ALTER TABLE \"Words\" RENAME TO \"WordsLegacy\"");

                var legacyIndexes = connection.Query<SchemaIndex>(
                    "PRAGMA index_list(\"WordsLegacy\")");
                foreach (var index in legacyIndexes.Where(index =>
                    !index.Name.StartsWith(
                        "sqlite_autoindex",
                        StringComparison.OrdinalIgnoreCase)))
                {
                    connection.Execute(
                        $"DROP INDEX IF EXISTS {QuoteIdentifier(index.Name)}");
                }

                connection.CreateTable<WordEntry>();
                connection.Execute(
                    $$"""
                    INSERT INTO "Words" ({{targetColumns}})
                    SELECT {{sourceExpressions}}
                    FROM "WordsLegacy"
                    """);
                connection.Execute("DROP TABLE \"WordsLegacy\"");
            });
        }

        private async Task EnsureCurrentHintSchemaAsync()
        {
            var columns = await database.Connection.QueryAsync<SchemaColumn>(
                "PRAGMA table_info(\"Hints\")");
            if (columns.Count == 0)
            {
                await database.Connection.CreateTableAsync<HintEntry>();
                return;
            }

            var currentOrder = columns.Select(column => column.Name).ToList();
            if (currentOrder.SequenceEqual(
                HintColumnOrder,
                StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            await database.Connection.RunInTransactionAsync(connection =>
            {
                connection.Execute("DROP TABLE IF EXISTS \"Hints\"");
                connection.CreateTable<HintEntry>();
            });
        }

        private static string GetMigrationExpression(
            string targetColumn,
            IReadOnlySet<string> sourceColumns,
            bool hasLegacyIrregularScore)
        {
            if (hasLegacyIrregularScore &&
                targetColumn is nameof(WordEntry.ScoreIrregularPrateritum) or
                    nameof(WordEntry.ScoreIrregularPerfect))
            {
                return QuoteIdentifier("ScoreIrregular");
            }

            if (sourceColumns.Contains(targetColumn))
            {
                return QuoteIdentifier(targetColumn);
            }

            return targetColumn switch
            {
                nameof(WordEntry.Gender) or
                nameof(WordEntry.GenderHint) or
                nameof(WordEntry.Past) or
                nameof(WordEntry.Perfekt) or
                nameof(WordEntry.Plural) or
                nameof(WordEntry.Prefix) => "NULL",
                nameof(WordEntry.Word) or
                nameof(WordEntry.Translation) or
                nameof(WordEntry.Type) or
                nameof(WordEntry.Level) => "''",
                _ => "0"
            };
        }

        private static bool IsDefaultableSourceColumn(string column) =>
            column is nameof(WordEntry.ScorePlural) or
                nameof(WordEntry.MistakePlural);

        private static string QuoteIdentifier(string identifier) =>
            $"\"{identifier.Replace("\"", "\"\"")}\"";

        private sealed record SeedData(
            IReadOnlyList<WordEntry> Words,
            IReadOnlyList<HintEntry> Hints);

        public sealed class SchemaColumn
        {
            [Column("name")]
            public string Name { get; set; } = string.Empty;
        }

        public sealed class SchemaIndex
        {
            [Column("name")]
            public string Name { get; set; } = string.Empty;
        }
    }
}
