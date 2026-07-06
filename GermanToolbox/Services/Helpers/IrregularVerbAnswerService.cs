using System.Text.RegularExpressions;

namespace GermanToolbox
{
    public static partial class IrregularVerbAnswerService
    {
        public static string GetAnswer(WordEntry word, IrregularVerbForm form) =>
            form switch
            {
                IrregularVerbForm.Prateritum => word.Past ?? string.Empty,
                _ => word.Perfekt ?? string.Empty
            };

        public static string GetDisplayAnswer(WordEntry word, IrregularVerbForm form) =>
            string.Join(" / ", GetAcceptedAnswers(word, form));

        public static bool IsCorrect(
            WordEntry word,
            IrregularVerbForm form,
            string? response)
        {
            var normalizedResponse = Normalize(response);
            return Normalize(GetDisplayAnswer(word, form)) == normalizedResponse ||
                GetAcceptedAnswers(word, form)
                    .Any(answer => Normalize(answer) == normalizedResponse);
        }

        public static IReadOnlyList<string> GetAcceptedAnswers(
            WordEntry word,
            IrregularVerbForm form) =>
            GetAnswer(word, form)
                .Split(['/', ';', '|'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        public static string Normalize(string? value) =>
            WhitespaceRegex().Replace(value?.Trim().ToLowerInvariant() ?? string.Empty, " ");

        [GeneratedRegex(@"\s+")]
        private static partial Regex WhitespaceRegex();
    }
}
