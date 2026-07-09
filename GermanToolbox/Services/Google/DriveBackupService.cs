using System.IO.Compression;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Net.Http;
using System.Linq;
using Microsoft.Maui.Storage;

namespace GermanToolbox
{
    public sealed class DriveBackupService
    {
        private static readonly string[] RestorableProgressColumns = WordRepository.UserProgressColumnOrder
            .Skip(1)
            .ToArray();

        private readonly WordRepository wordRepository;
        private readonly PracticeSettingsService settingsService;
        private readonly GoogleAuthService googleAuthService;
        private readonly HttpClient httpClient = new HttpClient();

        public bool IsRestoreInProgress { get; private set; }

        public DriveBackupService(
            WordRepository wordRepository,
            PracticeSettingsService settingsService,
            GoogleAuthService googleAuthService)
        {
            this.wordRepository = wordRepository;
            this.settingsService = settingsService;
            this.googleAuthService = googleAuthService;
        }

        public async Task<byte[]> CreateBackupZipAsync(IProgress<int>? progress = null)
        {
            progress?.Report(0);
            var rows = await wordRepository.GetUserProgressRowsAsync();
            progress?.Report(30);

            // Build CSV for words
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", WordRepository.UserProgressColumnOrder));
            foreach (var r in rows)
            {
                sb.AppendLine(string.Join(",",
                    r.Id,
                    r.Learning ? 1 : 0,
                    r.ScoreMeaning,
                    r.ScoreReverseMeaning,
                    r.ScoreArticle,
                    r.ScorePlural,
                    r.ScoreIrregularPrateritum,
                    r.ScoreIrregularPerfect,
                    r.MistakeMeaning ? 1 : 0,
                    r.MistakeArticle ? 1 : 0,
                    r.MistakePlural ? 1 : 0,
                    r.MistakeIrregular ? 1 : 0));
            }

            progress?.Report(60);

            // Settings as JSON (enums serialized as strings)
            var settingsObj = new
            {
                LearnedThreshold = settingsService.LearnedThreshold,
                TestChunkSize = settingsService.TestChunkSize,
                SoundsEnabled = settingsService.SoundsEnabled,
                VibrationsEnabled = settingsService.VibrationsEnabled,
                VocabularyLevel = settingsService.VocabularyLevel,
                VocabularyDirection = settingsService.VocabularyDirection.ToString(),
                ArticleLevel = settingsService.ArticleLevel,
                SelectedArticleCase = settingsService.SelectedArticleCase.ToString(),
                SelectedArticleType = settingsService.SelectedArticleType.ToString(),
                PluralLevel = settingsService.PluralLevel,
                SelectedPluralTestMethod = settingsService.SelectedPluralTestMethod.ToString(),
                IrregularVerbLevel = settingsService.IrregularVerbLevel,
                SelectedIrregularVerbForm = settingsService.SelectedIrregularVerbForm.ToString(),
                SelectedIrregularTestMethod = settingsService.SelectedIrregularTestMethod.ToString()
            };

            var settingsJson = JsonSerializer.Serialize(settingsObj, new JsonSerializerOptions { WriteIndented = false });

            var manifestObj = new { Version = 1, Timestamp = DateTimeOffset.UtcNow };
            var manifestJson = JsonSerializer.Serialize(manifestObj, new JsonSerializerOptions { WriteIndented = false });

            await using var ms = new MemoryStream();
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                var csvEntry = archive.CreateEntry("words.csv", CompressionLevel.Optimal);
                await using (var csvStream = csvEntry.Open())
                {
                    var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                    await csvStream.WriteAsync(bytes, 0, bytes.Length);
                }

                var settingsEntry = archive.CreateEntry("settings.json", CompressionLevel.Optimal);
                await using (var settingsStream = settingsEntry.Open())
                {
                    var bytes = Encoding.UTF8.GetBytes(settingsJson);
                    await settingsStream.WriteAsync(bytes, 0, bytes.Length);
                }

                var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
                await using (var manifestStream = manifestEntry.Open())
                {
                    var bytes = Encoding.UTF8.GetBytes(manifestJson);
                    await manifestStream.WriteAsync(bytes, 0, bytes.Length);
                }
            }

            progress?.Report(100);
            return ms.ToArray();
        }

        public async Task<string> UploadBackupAsync(byte[] zipBytes, string fileName)
        {
            var uploadUrl = "https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart&fields=id,name,modifiedTime";
            var payload = await SendDriveJsonRequestAsync(
                () => new HttpRequestMessage(HttpMethod.Post, uploadUrl)
                {
                    Content = CreateMultipartRelatedContent(fileName, zipBytes)
                },
                canRetryWithRefresh: true);

            using var doc = JsonDocument.Parse(payload);
            return doc.RootElement.GetProperty("id").GetString() ?? string.Empty;
        }

        public async Task<IReadOnlyList<DriveBackupItem>> ListBackupsAsync()
        {
            var url = "https://www.googleapis.com/drive/v3/files?spaces=appDataFolder&orderBy=modifiedTime desc&pageSize=50&fields=files(id,name,modifiedTime,size)";
            var payload = await SendDriveJsonRequestAsync(() => new HttpRequestMessage(HttpMethod.Get, url), canRetryWithRefresh: true);

            using var doc = JsonDocument.Parse(payload);
            var files = doc.RootElement.GetProperty("files").EnumerateArray()
                .Select(f => new DriveBackupItem(
                    f.GetProperty("id").GetString() ?? string.Empty,
                    f.GetProperty("name").GetString() ?? string.Empty,
                    f.TryGetProperty("modifiedTime", out var m) ? m.GetDateTimeOffset() : DateTimeOffset.MinValue,
                    f.TryGetProperty("size", out var s) ? ReadInt64Value(s) : 0))
                .ToList();

            return files;
        }

        public async Task<byte[]> DownloadBackupAsync(string fileId)
        {
            var url = $"https://www.googleapis.com/drive/v3/files/{fileId}?alt=media";
            return await SendDriveBinaryRequestAsync(() => new HttpRequestMessage(HttpMethod.Get, url), canRetryWithRefresh: true);
        }

        private MultipartContent CreateMultipartRelatedContent(string fileName, byte[] zipBytes)
        {
            var metadata = JsonSerializer.Serialize(new { name = fileName, parents = new[] { "appDataFolder" } });
            var boundary = $"boundary_{Guid.NewGuid():N}";
            var content = new MultipartContent("related", boundary);

            content.Add(new StringContent(metadata, Encoding.UTF8, "application/json"));
            var fileContent = new ByteArrayContent(zipBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
            content.Add(fileContent);

            return content;
        }

        private async Task<string> SendDriveJsonRequestAsync(
            Func<HttpRequestMessage> requestFactory,
            bool canRetryWithRefresh)
        {
            var response = await SendAuthorizedRequestAsync(requestFactory);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && canRetryWithRefresh)
            {
                var refreshed = await SendAuthorizedRequestAsync(requestFactory, forceRefresh: true);
                response.Dispose();
                response = refreshed;
            }

            using (response)
            {
                var payloadBytes = await response.Content.ReadAsByteArrayAsync();
                if (!response.IsSuccessStatusCode)
                {
                    var payload = Encoding.UTF8.GetString(payloadBytes);
                    throw new InvalidOperationException($"Google Drive request failed: {payload}");
                }

                return Encoding.UTF8.GetString(payloadBytes);
            }
        }

        private async Task<byte[]> SendDriveBinaryRequestAsync(
            Func<HttpRequestMessage> requestFactory,
            bool canRetryWithRefresh)
        {
            var response = await SendAuthorizedRequestAsync(requestFactory);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && canRetryWithRefresh)
            {
                var refreshed = await SendAuthorizedRequestAsync(requestFactory, forceRefresh: true);
                response.Dispose();
                response = refreshed;
            }

            using (response)
            {
                var payloadBytes = await response.Content.ReadAsByteArrayAsync();
                if (!response.IsSuccessStatusCode)
                {
                    var payload = Encoding.UTF8.GetString(payloadBytes);
                    throw new InvalidOperationException($"Google Drive download failed: {payload}");
                }

                return payloadBytes;
            }
        }

        private async Task<HttpResponseMessage> SendAuthorizedRequestAsync(
            Func<HttpRequestMessage> requestFactory,
            bool forceRefresh = false)
        {
            var accessToken = await googleAuthService.GetValidAccessTokenAsync(forceRefresh);
            var request = requestFactory();
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            return await httpClient.SendAsync(request);
        }

        public async Task<DriveBackupRestorePlan> CreateRestorePlanAsync(byte[] zipBytes)
        {
            using var ms = new MemoryStream(zipBytes);
            using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
            var csvEntry = archive.GetEntry("words.csv");
            if (csvEntry is null)
            {
                throw new InvalidOperationException("Backup archive missing words.csv");
            }

            using var stream = csvEntry.Open();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var header = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(header))
            {
                throw new InvalidOperationException("Backup archive words.csv is missing a header row.");
            }

            var csvColumns = SplitCsvLine(header)
                .Where(column => !string.IsNullOrWhiteSpace(column))
                .ToList();
            var csvColumnSet = csvColumns.ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!csvColumnSet.Contains(nameof(WordEntry.Id)))
            {
                throw new InvalidOperationException("Backup archive words.csv is missing the Id column.");
            }

            var expectedColumnSet = RestorableProgressColumns.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var matchingColumns = RestorableProgressColumns
                .Where(csvColumnSet.Contains)
                .ToList();
            var missingColumns = RestorableProgressColumns
                .Where(column => !csvColumnSet.Contains(column))
                .ToList();
            var unexpectedColumns = csvColumns
                .Where(column =>
                    !string.Equals(column, nameof(WordEntry.Id), StringComparison.OrdinalIgnoreCase) &&
                    !expectedColumnSet.Contains(column))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new DriveBackupRestorePlan(
                matchingColumns,
                missingColumns,
                unexpectedColumns);
        }

        public async Task RestoreFromZipAsync(
            byte[] zipBytes,
            IReadOnlyCollection<string> columnsToRestore,
            IProgress<int>? progress = null)
        {
            if (IsRestoreInProgress)
            {
                throw new InvalidOperationException("A restore is already in progress.");
            }

            try
            {
                IsRestoreInProgress = true;
                progress?.Report(0);

                using var ms = new MemoryStream(zipBytes);
                using var archive = new ZipArchive(ms, ZipArchiveMode.Read);

                var csvEntry = archive.GetEntry("words.csv");
                if (csvEntry is null)
                {
                    throw new InvalidOperationException("Backup archive missing words.csv");
                }

                using var s = csvEntry.Open();
                using var reader = new StreamReader(s, Encoding.UTF8);
                var rows = new List<WordRepository.WordProgressRow>();
                var header = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(header))
                {
                    throw new InvalidOperationException("Backup archive words.csv is missing a header row.");
                }

                var headerIndexes = GetHeaderIndexes(header);
                if (!headerIndexes.ContainsKey(nameof(WordEntry.Id)))
                {
                    throw new InvalidOperationException("Backup archive words.csv is missing the Id column.");
                }

                var updateColumns = NormalizeRestoreColumns(columnsToRestore, headerIndexes.Keys);
                if (updateColumns.Count > 0)
                {
                    string? line;
                    while ((line = await reader.ReadLineAsync()) is not null)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }

                        var parts = SplitCsvLine(line);
                        if (TryReadProgressRow(parts, headerIndexes, updateColumns, out var row))
                        {
                            rows.Add(row);
                        }
                    }
                }

                progress?.Report(70);

                await wordRepository.ApplyUserProgressAsync(rows, updateColumns);

                // Restore settings if present
                var settingsEntry = archive.GetEntry("settings.json");
                if (settingsEntry is not null)
                {
                    using var settingsStream = settingsEntry.Open();
                    using var settingsReader = new StreamReader(settingsStream, Encoding.UTF8);
                    var settingsJson = await settingsReader.ReadToEndAsync();
                    try
                    {
                        using var settingsDoc = JsonDocument.Parse(settingsJson);
                        var root = settingsDoc.RootElement;

                        if (TryReadInt32(root, "LearnedThreshold", out var learnedThreshold))
                        {
                            settingsService.LearnedThreshold = learnedThreshold;
                        }

                        if (TryReadInt32(root, "TestChunkSize", out var testChunkSize))
                        {
                            settingsService.TestChunkSize = testChunkSize;
                        }

                        if (TryReadBoolean(root, "SoundsEnabled", out var soundsEnabled))
                        {
                            settingsService.SoundsEnabled = soundsEnabled;
                        }

                        if (TryReadBoolean(root, "VibrationsEnabled", out var vibrationsEnabled))
                        {
                            settingsService.VibrationsEnabled = vibrationsEnabled;
                        }

                        if (TryReadString(root, "VocabularyLevel", out var lvl))
                        {
                            settingsService.VocabularyLevel = lvl;
                        }

                        if (TryReadString(root, "VocabularyDirection", out var dirStr) &&
                            Enum.TryParse<VocabularyTestDirection>(dirStr, out var dir))
                        {
                            settingsService.VocabularyDirection = dir;
                        }

                        if (TryReadString(root, "ArticleLevel", out var alevel))
                        {
                            settingsService.ArticleLevel = alevel;
                        }

                        if (TryReadString(root, "SelectedArticleCase", out var acase) &&
                            Enum.TryParse<ArticleCase>(acase, out var articleCase))
                        {
                            settingsService.SelectedArticleCase = articleCase;
                        }

                        if (TryReadString(root, "SelectedArticleType", out var atype) &&
                            Enum.TryParse<ArticleType>(atype, out var articleType))
                        {
                            settingsService.SelectedArticleType = articleType;
                        }

                        if (TryReadString(root, "PluralLevel", out var plevel))
                        {
                            settingsService.PluralLevel = plevel;
                        }

                        if (TryReadString(root, "SelectedPluralTestMethod", out var pmethod) &&
                            Enum.TryParse<IrregularTestMethod>(pmethod, out var pluralMethod))
                        {
                            settingsService.SelectedPluralTestMethod = pluralMethod;
                        }

                        if (TryReadString(root, "IrregularVerbLevel", out var ilevel))
                        {
                            settingsService.IrregularVerbLevel = ilevel;
                        }

                        if (TryReadString(root, "SelectedIrregularVerbForm", out var ivform) &&
                            Enum.TryParse<IrregularVerbForm>(ivform, out var ivf))
                        {
                            settingsService.SelectedIrregularVerbForm = ivf;
                        }

                        if (TryReadString(root, "SelectedIrregularTestMethod", out var imethod) &&
                            Enum.TryParse<IrregularTestMethod>(imethod, out var im))
                        {
                            settingsService.SelectedIrregularTestMethod = im;
                        }
                    }
                    catch
                    {
                        // ignore settings parse errors
                    }
                }

                progress?.Report(100);
            }
            finally
            {
                IsRestoreInProgress = false;
            }
        }

        private static IReadOnlyList<string> NormalizeRestoreColumns(
            IReadOnlyCollection<string> requestedColumns,
            IEnumerable<string> csvColumns)
        {
            var requestedColumnSet = requestedColumns.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var csvColumnSet = csvColumns.ToHashSet(StringComparer.OrdinalIgnoreCase);
            return RestorableProgressColumns
                .Where(column =>
                    requestedColumnSet.Contains(column) &&
                    csvColumnSet.Contains(column))
                .ToList();
        }

        private static Dictionary<string, int> GetHeaderIndexes(string header)
        {
            var columns = SplitCsvLine(header);
            var indexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < columns.Length; index++)
            {
                if (!string.IsNullOrWhiteSpace(columns[index]) && !indexes.ContainsKey(columns[index]))
                {
                    indexes[columns[index]] = index;
                }
            }

            return indexes;
        }

        private static string[] SplitCsvLine(string line) =>
            line.Split(',').Select(part => part.Trim()).ToArray();

        private static bool TryReadProgressRow(
            string[] parts,
            IReadOnlyDictionary<string, int> headerIndexes,
            IReadOnlyList<string> updateColumns,
            out WordRepository.WordProgressRow row)
        {
            row = new WordRepository.WordProgressRow();
            if (!TryGetCsvValue(parts, headerIndexes, nameof(WordEntry.Id), out var idText) ||
                !int.TryParse(idText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                return false;
            }

            row.Id = id;
            foreach (var column in updateColumns)
            {
                if (!TryGetCsvValue(parts, headerIndexes, column, out var value) ||
                    !TrySetProgressColumnValue(row, column, value))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryGetCsvValue(
            string[] parts,
            IReadOnlyDictionary<string, int> headerIndexes,
            string column,
            out string value)
        {
            value = string.Empty;
            if (!headerIndexes.TryGetValue(column, out var index) || index >= parts.Length)
            {
                return false;
            }

            value = parts[index];
            return true;
        }

        private static bool TrySetProgressColumnValue(
            WordRepository.WordProgressRow row,
            string column,
            string value)
        {
            switch (column)
            {
                case nameof(WordEntry.Learning):
                    if (!TryReadBooleanFlag(value, out var learning))
                    {
                        return false;
                    }

                    row.Learning = learning;
                    return true;
                case nameof(WordEntry.ScoreMeaning):
                    if (!TryReadInt32(value, out var scoreMeaning))
                    {
                        return false;
                    }

                    row.ScoreMeaning = scoreMeaning;
                    return true;
                case nameof(WordEntry.ScoreReverseMeaning):
                    if (!TryReadInt32(value, out var scoreReverseMeaning))
                    {
                        return false;
                    }

                    row.ScoreReverseMeaning = scoreReverseMeaning;
                    return true;
                case nameof(WordEntry.ScoreArticle):
                    if (!TryReadInt32(value, out var scoreArticle))
                    {
                        return false;
                    }

                    row.ScoreArticle = scoreArticle;
                    return true;
                case nameof(WordEntry.ScorePlural):
                    if (!TryReadInt32(value, out var scorePlural))
                    {
                        return false;
                    }

                    row.ScorePlural = scorePlural;
                    return true;
                case nameof(WordEntry.ScoreIrregularPrateritum):
                    if (!TryReadInt32(value, out var scoreIrregularPrateritum))
                    {
                        return false;
                    }

                    row.ScoreIrregularPrateritum = scoreIrregularPrateritum;
                    return true;
                case nameof(WordEntry.ScoreIrregularPerfect):
                    if (!TryReadInt32(value, out var scoreIrregularPerfect))
                    {
                        return false;
                    }

                    row.ScoreIrregularPerfect = scoreIrregularPerfect;
                    return true;
                case nameof(WordEntry.MistakeMeaning):
                    if (!TryReadBooleanFlag(value, out var mistakeMeaning))
                    {
                        return false;
                    }

                    row.MistakeMeaning = mistakeMeaning;
                    return true;
                case nameof(WordEntry.MistakeArticle):
                    if (!TryReadBooleanFlag(value, out var mistakeArticle))
                    {
                        return false;
                    }

                    row.MistakeArticle = mistakeArticle;
                    return true;
                case nameof(WordEntry.MistakePlural):
                    if (!TryReadBooleanFlag(value, out var mistakePlural))
                    {
                        return false;
                    }

                    row.MistakePlural = mistakePlural;
                    return true;
                case nameof(WordEntry.MistakeIrregular):
                    if (!TryReadBooleanFlag(value, out var mistakeIrregular))
                    {
                        return false;
                    }

                    row.MistakeIrregular = mistakeIrregular;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryReadBooleanFlag(string value, out bool result)
        {
            if (value == "1")
            {
                result = true;
                return true;
            }

            if (value == "0")
            {
                result = false;
                return true;
            }

            return bool.TryParse(value, out result);
        }

        private static bool TryReadInt32(string value, out int result) =>
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

        private static bool TryReadString(JsonElement root, string propertyName, out string value)
        {
            value = string.Empty;
            if (!root.TryGetProperty(propertyName, out var property))
            {
                return false;
            }

            if (property.ValueKind == JsonValueKind.String)
            {
                value = property.GetString() ?? string.Empty;
                return !string.IsNullOrWhiteSpace(value);
            }

            value = property.ToString();
            return !string.IsNullOrWhiteSpace(value);
        }

        private static bool TryReadInt32(JsonElement root, string propertyName, out int value)
        {
            value = 0;
            if (!root.TryGetProperty(propertyName, out var property))
            {
                return false;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out value))
            {
                return true;
            }

            if (property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), out value))
            {
                return true;
            }

            return false;
        }

        private static bool TryReadBoolean(JsonElement root, string propertyName, out bool value)
        {
            value = false;
            if (!root.TryGetProperty(propertyName, out var property))
            {
                return false;
            }

            if (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False)
            {
                value = property.GetBoolean();
                return true;
            }

            if (property.ValueKind == JsonValueKind.String && bool.TryParse(property.GetString(), out value))
            {
                return true;
            }

            return false;
        }

        private static long ReadInt64Value(JsonElement property)
        {
            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var number))
            {
                return number;
            }

            if (property.ValueKind == JsonValueKind.String && long.TryParse(property.GetString(), out var parsed))
            {
                return parsed;
            }

            return 0;
        }
    }

    public sealed record DriveBackupItem(string Id, string Name, DateTimeOffset Modified, long Size);

    public sealed record DriveBackupRestorePlan(
        IReadOnlyList<string> MatchingColumns,
        IReadOnlyList<string> MissingColumns,
        IReadOnlyList<string> UnexpectedColumns)
    {
        public bool HasColumnMismatch => MissingColumns.Count > 0 || UnexpectedColumns.Count > 0;
    }
}
