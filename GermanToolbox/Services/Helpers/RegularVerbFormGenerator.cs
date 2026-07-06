namespace GermanToolbox
{
    public sealed class RegularVerbFormGenerator
    {
        private static readonly string[] SeparablePrefixes =
        [
            "auseinander", "beiseite", "durcheinander", "gegenüber", "herunter",
            "hinunter", "zurück", "zusammen", "entgegen", "entlang", "heraus",
            "hinein", "vorbei", "weiter", "wieder", "ab", "an", "auf", "aus",
            "bei", "ein", "fest", "fort", "her", "hin", "hoch", "los", "mit",
            "nach", "statt", "teil", "umher", "vor", "weg", "zu"
        ];

        private static readonly string[] InseparablePrefixes =
        [
            "be", "emp", "ent", "er", "ge", "miss", "ver", "zer"
        ];

        public RegularVerbForms Generate(string infinitivePhrase)
        {
            var words = infinitivePhrase
                .Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                return new RegularVerbForms(string.Empty, string.Empty);
            }

            var infinitive = words[^1];
            var context = words[..^1];
            var (separablePrefix, root) = SplitSeparablePrefix(infinitive);
            var stem = GetStem(root);
            var prateritum = stem + GetPastEnding(stem);
            var perfect = CreateParticiple(infinitive, separablePrefix, stem);

            return new RegularVerbForms(
                BuildFinitePhrase(prateritum, separablePrefix, context),
                perfect);
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
            string stem)
        {
            var ending = GetPresentEnding(stem);
            if (!string.IsNullOrEmpty(separablePrefix))
            {
                return separablePrefix + "ge" + stem + ending;
            }

            if (infinitive.EndsWith("ieren", StringComparison.OrdinalIgnoreCase) ||
                InseparablePrefixes.Any(prefix =>
                    infinitive.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                    infinitive.Length > prefix.Length + 2))
            {
                return stem + ending;
            }

            return "ge" + stem + ending;
        }

        private static (string Prefix, string Root) SplitSeparablePrefix(string infinitive)
        {
            var prefix = SeparablePrefixes
                .Where(item => infinitive.StartsWith(item, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.Length)
                .FirstOrDefault(item => infinitive.Length > item.Length + 2);

            return prefix is null
                ? (string.Empty, infinitive)
                : (infinitive[..prefix.Length], infinitive[prefix.Length..]);
        }

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
