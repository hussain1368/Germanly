using System.Text.RegularExpressions;

namespace GermanToolbox
{
    public static partial class PluralAnswerService
    {
        public static IReadOnlyList<string> GetAcceptedAnswers(WordEntry word) =>
            (word.Plural ?? string.Empty)
                .Split(
                    ';',
                    StringSplitOptions.TrimEntries |
                    StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        public static string GetDisplayAnswer(WordEntry word) =>
            string.Join(" / ", GetAcceptedAnswers(word));

        public static bool IsCorrect(WordEntry word, string? response)
        {
            var normalizedResponse = Normalize(response);
            return GetAcceptedAnswers(word)
                .Any(answer => Normalize(answer) == normalizedResponse);
        }

        public static string Normalize(string? value) =>
            WhitespaceRegex().Replace(
                value?.Trim().ToLowerInvariant() ?? string.Empty,
                " ");

        [GeneratedRegex(@"\s+")]
        private static partial Regex WhitespaceRegex();
    }
}
