namespace GermanToolbox
{
    public sealed class RegularVerbFormGenerator
    {
        public RegularVerbForms Generate(WordEntry word) =>
            Generate(word.Word, word.IsSeparable, word.Prefix);

        public RegularVerbForms Generate(
            string infinitivePhrase,
            bool isSeparable,
            string? prefix)
        {
            var words = infinitivePhrase
                .Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                return new RegularVerbForms(string.Empty, string.Empty);
            }

            var infinitive = words[^1];
            var context = words[..^1];
            var separablePrefix = ResolveSeparablePrefix(infinitive, isSeparable, prefix);
            var root = string.IsNullOrEmpty(separablePrefix)
                ? infinitive
                : infinitive[separablePrefix.Length..];
            var stem = GetStem(root);
            var prateritum = stem + GetPastEnding(stem);
            var perfect = CreateParticiple(
                infinitive,
                separablePrefix,
                stem,
                isSeparable,
                prefix);

            return new RegularVerbForms(
                BuildFinitePhrase(prateritum, separablePrefix, context),
                perfect);
        }

        private static string ResolveSeparablePrefix(
            string infinitive,
            bool isSeparable,
            string? prefix)
        {
            if (!isSeparable || string.IsNullOrWhiteSpace(prefix))
            {
                return string.Empty;
            }

            var candidate = prefix.Trim();
            if (!infinitive.StartsWith(candidate, StringComparison.OrdinalIgnoreCase) ||
                infinitive.Length <= candidate.Length)
            {
                return string.Empty;
            }

            return infinitive[..candidate.Length];
        }

        private static string BuildFinitePhrase(
            string finiteVerb,
            string separablePrefix,
            IReadOnlyList<string> context)
        {
            var parts = new List<string> { finiteVerb };
            parts.AddRange(context);
            if (!string.IsNullOrEmpty(separablePrefix))
            {
                parts.Add(separablePrefix);
            }

            return string.Join(' ', parts);
        }

        private static string CreateParticiple(
            string infinitive,
            string separablePrefix,
            string stem,
            bool isSeparable,
            string? prefix)
        {
            var ending = GetPresentEnding(stem);
            if (!string.IsNullOrEmpty(separablePrefix))
            {
                return separablePrefix + "ge" + stem + ending;
            }

            if (infinitive.EndsWith("ieren", StringComparison.OrdinalIgnoreCase) ||
                HasInseparablePrefix(isSeparable, prefix))
            {
                return stem + ending;
            }

            return "ge" + stem + ending;
        }

        private static bool HasInseparablePrefix(bool isSeparable, string? prefix) =>
            !isSeparable && !string.IsNullOrWhiteSpace(prefix);

        private static string GetStem(string infinitive)
        {
            if (infinitive.EndsWith("en", StringComparison.OrdinalIgnoreCase))
            {
                return infinitive[..^2];
            }

            return infinitive.EndsWith('n') ? infinitive[..^1] : infinitive;
        }

        private static string GetPresentEnding(string stem) =>
            NeedsConnectingE(stem) ? "et" : "t";

        private static string GetPastEnding(string stem) =>
            NeedsConnectingE(stem) ? "ete" : "te";

        private static bool NeedsConnectingE(string stem)
        {
            if (stem.EndsWith('d') || stem.EndsWith('t'))
            {
                return true;
            }

            if (stem.Length < 2 || (stem[^1] is not 'm' and not 'n'))
            {
                return false;
            }

            var precedingCharacter = char.ToLowerInvariant(stem[^2]);
            return !"aeiouäöülrmn".Contains(precedingCharacter);
        }
    }
}
