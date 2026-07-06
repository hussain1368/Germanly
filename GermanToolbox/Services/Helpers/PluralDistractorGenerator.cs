namespace GermanToolbox
{
    public sealed class PluralDistractorGenerator
    {
        private static readonly string[] InvariantEndings =
        [
            "chen",
            "lein"
        ];

        public IReadOnlyList<string> Generate(
            WordEntry word,
            int maximumCount = 3)
        {
            if (maximumCount <= 0 || string.IsNullOrWhiteSpace(word.Word))
            {
                return [];
            }

            var answers = PluralAnswerService.GetAcceptedAnswers(word);
            var excludedAnswers = answers
                .Select(PluralAnswerService.Normalize)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var answer in answers.Where(answer =>
                !answer.EndsWith('n') &&
                !answer.EndsWith('s')))
            {
                excludedAnswers.Add(
                    PluralAnswerService.Normalize(answer + "n"));
            }

            if (excludedAnswers.Count == 0)
            {
                return [];
            }

            var candidates = new List<string>();
            AddCandidate(candidates, word.Word.Trim());
            AddRemovedUmlautForms(
                candidates,
                word.Word.Trim(),
                answers);
            AddCommonPatternForms(candidates, word.Word.Trim());
            AddUmlautTraps(
                candidates,
                word.Word.Trim());

            return candidates
                .Where(candidate => !excludedAnswers.Contains(
                    PluralAnswerService.Normalize(candidate)))
                .DistinctBy(
                    PluralAnswerService.Normalize,
                    StringComparer.OrdinalIgnoreCase)
                .Take(maximumCount)
                .ToList();
        }

        private static void AddCommonPatternForms(
            ICollection<string> candidates,
            string singular)
        {
            if (singular.EndsWith("in", StringComparison.OrdinalIgnoreCase))
            {
                AddCandidate(candidates, singular + "nen");
                AddCandidate(candidates, singular + "en");
                AddCandidate(candidates, singular + "s");
                AddCandidate(candidates, singular + "e");
                return;
            }

            if (singular.EndsWith('e'))
            {
                AddCandidate(candidates, singular + "n");
                AddCandidate(candidates, singular + "s");
                AddCandidate(candidates, singular + "r");
                AddCandidate(candidates, singular + "ns");
                return;
            }

            if (singular.EndsWith("um", StringComparison.OrdinalIgnoreCase) &&
                singular.Length > 2)
            {
                AddCandidate(candidates, singular[..^2] + "en");
                AddCandidate(candidates, singular + "s");
                AddCandidate(candidates, singular + "e");
                AddCandidate(candidates, singular + "en");
                return;
            }

            if (InvariantEndings.Any(ending =>
                singular.EndsWith(ending, StringComparison.OrdinalIgnoreCase)))
            {
                AddCandidate(candidates, singular + "s");
                AddCandidate(candidates, singular + "e");
                AddCandidate(candidates, singular + "er");
                AddCandidate(candidates, singular + "en");
                return;
            }

            if (singular.EndsWith("er", StringComparison.OrdinalIgnoreCase) ||
                singular.EndsWith("el", StringComparison.OrdinalIgnoreCase))
            {
                AddCandidate(candidates, singular + "s");
                AddCandidate(candidates, singular + "e");
                AddCandidate(candidates, singular + "en");
                AddCandidate(candidates, singular + "er");
                return;
            }

            if (singular.EndsWith("en", StringComparison.OrdinalIgnoreCase))
            {
                AddCandidate(candidates, singular + "s");
                AddCandidate(candidates, singular + "e");
                AddCandidate(candidates, singular + "er");
                return;
            }

            AddCandidate(candidates, singular + "e");
            AddCandidate(candidates, singular + "en");
            if (!EndsWithSibilant(singular))
            {
                AddCandidate(candidates, singular + "s");
            }
            else
            {
                AddCandidate(candidates, singular + "es");
            }
            AddCandidate(candidates, singular + "er");
        }

        private static void AddRemovedUmlautForms(
            ICollection<string> candidates,
            string singular,
            IReadOnlyList<string> acceptedAnswers)
        {
            if (HasUmlaut(singular))
            {
                return;
            }

            foreach (var answer in acceptedAnswers)
            {
                AddCandidate(candidates, RemoveUmlauts(answer));
            }
        }

        private static void AddUmlautTraps(
            ICollection<string> candidates,
            string singular)
        {
            var umlauted = AddLikelyUmlaut(singular);
            if (umlauted is not null)
            {
                AddCandidate(candidates, umlauted);
                AddCandidate(candidates, umlauted + "e");
                AddCandidate(candidates, umlauted + "er");
                AddCandidate(candidates, umlauted + "en");
            }
        }

        private static bool EndsWithSibilant(string value) =>
            value.EndsWith('s') ||
            value.EndsWith('ß') ||
            value.EndsWith('x') ||
            value.EndsWith('z');

        private static bool HasUmlaut(string value) =>
            value.IndexOfAny(['ä', 'ö', 'ü', 'Ä', 'Ö', 'Ü']) >= 0;

        private static string? AddLikelyUmlaut(string value)
        {
            if (HasUmlaut(value))
            {
                return null;
            }

            var stemEnd = value.Length;
            foreach (var ending in new[] { "ern", "eln", "en", "er", "el" })
            {
                if (value.EndsWith(ending, StringComparison.OrdinalIgnoreCase) &&
                    value.Length > ending.Length + 1)
                {
                    stemEnd -= ending.Length;
                    break;
                }
            }

            for (var index = stemEnd - 2; index >= 0; index--)
            {
                if (string.Equals(
                    value.Substring(index, 2),
                    "au",
                    StringComparison.OrdinalIgnoreCase))
                {
                    var replacement = char.IsUpper(value[index]) ? "Äu" : "äu";
                    return value.Remove(index, 2).Insert(index, replacement);
                }
            }

            for (var index = stemEnd - 1; index >= 0; index--)
            {
                var replacement = value[index] switch
                {
                    'a' => 'ä',
                    'o' => 'ö',
                    'u' => 'ü',
                    'A' => 'Ä',
                    'O' => 'Ö',
                    'U' => 'Ü',
                    _ => '\0'
                };
                if (replacement != '\0')
                {
                    return value.Remove(index, 1).Insert(index, replacement.ToString());
                }
            }

            return null;
        }

        private static string RemoveUmlauts(string value)
        {
            var result = value
                .Replace("ä", "a", StringComparison.Ordinal)
                .Replace("ö", "o", StringComparison.Ordinal)
                .Replace("ü", "u", StringComparison.Ordinal)
                .Replace("Ä", "A", StringComparison.Ordinal)
                .Replace("Ö", "O", StringComparison.Ordinal)
                .Replace("Ü", "U", StringComparison.Ordinal);
            return result;
        }

        private static void AddCandidate(
            ICollection<string> candidates,
            string candidate)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                candidates.Add(candidate.Trim());
            }
        }
    }
}
