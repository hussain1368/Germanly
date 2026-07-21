namespace GermanToolbox
{
    public sealed class IrregularVerbDistractorGenerator
    {
        /// <summary>
        /// Common ablaut / umlaut substitutions found among strong verbs.
        /// Longer sources are matched first (ie before i, ei before e, au before a).
        /// </summary>
        private static readonly (string From, string To)[] VowelChanges =
        [
            ("ie", "ei"),
            ("ie", "o"),
            ("ie", "a"),
            ("ie", "e"),
            ("ei", "ie"),
            ("ei", "i"),
            ("au", "ie"),
            ("äu", "au"),
            ("ä", "a"),
            ("ö", "o"),
            ("ü", "u"),
            ("ü", "o"),
            ("ü", "i"),
            ("ü", "a"),
            ("a", "ä"),
            ("a", "ie"),
            ("a", "u"),
            ("a", "i"),
            ("a", "o"),
            ("e", "a"),
            ("e", "o"),
            ("e", "i"),
            ("e", "ie"),
            ("e", "u"),
            ("i", "a"),
            ("i", "u"),
            ("i", "e"),
            ("i", "o"),
            ("o", "a"),
            ("o", "ö"),
            ("u", "a"),
            ("u", "ie"),
            ("u", "ü")
        ];

        public IReadOnlyList<string> Generate(
            WordEntry word,
            IrregularVerbForm form,
            int maximumCount = 3)
        {
            var correctAnswers = IrregularVerbAnswerService
                .GetAcceptedAnswers(word, form)
                .Select(IrregularVerbAnswerService.Normalize)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var primaryAnswer = IrregularVerbAnswerService
                .GetAcceptedAnswers(word, form)
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(primaryAnswer) || maximumCount <= 0)
            {
                return [];
            }

            var shape = Analyze(word);
            var candidates = new List<string>();

            if (form == IrregularVerbForm.Prateritum)
            {
                AddPrateritumDistractors(candidates, word, shape, primaryAnswer);
            }
            else
            {
                AddPerfectDistractors(candidates, word, shape, primaryAnswer);
            }

            return candidates
                .Where(candidate => !correctAnswers.Contains(
                    IrregularVerbAnswerService.Normalize(candidate)))
                .DistinctBy(
                    IrregularVerbAnswerService.Normalize,
                    StringComparer.OrdinalIgnoreCase)
                .Take(maximumCount)
                .ToList();
        }

        private static void AddPrateritumDistractors(
            ICollection<string> candidates,
            WordEntry word,
            VerbShape shape,
            string correctAnswer)
        {
            // Weak regularization: root stem + -te/-ete (+ separable particle).
            AddCandidate(
                candidates,
                BuildPrateritumPhrase(shape.Stem + GetPastEnding(shape.Stem), correctAnswer, shape));
            AddCandidate(
                candidates,
                BuildPrateritumPhrase(shape.Stem + "ete", correctAnswer, shape));
            AddCandidate(
                candidates,
                BuildPrateritumPhrase(shape.Stem + "te", correctAnswer, shape));

            // Ablaut / umlaut variants of the correct finite form.
            AddCandidate(candidates, RemoveUmlauts(shape, correctAnswer, IrregularVerbForm.Prateritum));
            foreach (var vowelCandidate in CreateVowelCandidates(
                shape,
                correctAnswer,
                IrregularVerbForm.Prateritum))
            {
                AddCandidate(candidates, vowelCandidate);
            }

            // Ending tweaks on the correct form (strong look-alike mistakes).
            AddCandidate(candidates, ChangeEnding(correctAnswer, IrregularVerbForm.Prateritum));

            // Use the perfect stem as if it were a past form.
            if (!string.IsNullOrWhiteSpace(word.Perfekt))
            {
                var perfectStem = ExtractMutableStem(
                    GetPrimaryToken(word.Perfekt!, isLast: true),
                    shape,
                    IrregularVerbForm.Perfect);
                if (!string.IsNullOrEmpty(perfectStem))
                {
                    AddCandidate(
                        candidates,
                        BuildPrateritumPhrase(perfectStem, correctAnswer, shape));
                    AddCandidate(
                        candidates,
                        BuildPrateritumPhrase(perfectStem + "e", correctAnswer, shape));
                }
            }

            // Bare infinitive stem / infinitive as a distractor.
            AddCandidate(
                candidates,
                BuildPrateritumPhrase(shape.Stem, correctAnswer, shape));
            AddCandidate(candidates, word.Word);
        }

        private static void AddPerfectDistractors(
            ICollection<string> candidates,
            WordEntry word,
            VerbShape shape,
            string correctAnswer)
        {
            // Pattern: ge + root-stem + en  (classic strong participle; wrong when no-ge / sep / weak)
            AddCandidate(
                candidates,
                ReplaceLastWord(correctAnswer, AssembleParticiple(shape, shape.Stem, withGe: true, "en")));

            // Pattern: ge + root-stem + t/et  (weak participle shell on strong verb)
            AddCandidate(
                candidates,
                ReplaceLastWord(
                    correctAnswer,
                    AssembleParticiple(shape, shape.Stem, withGe: true, GetPresentEnding(shape.Stem))));

            // Pattern: root + en  (no ge) — correct for many inseparable, wrong for plain/sep
            AddCandidate(
                candidates,
                ReplaceLastWord(correctAnswer, AssembleParticiple(shape, shape.Stem, withGe: false, "en")));

            // Pattern: root + t  (no ge) — inseparable weak-looking / -ieren-like
            AddCandidate(
                candidates,
                ReplaceLastWord(
                    correctAnswer,
                    AssembleParticiple(shape, shape.Stem, withGe: false, GetPresentEnding(shape.Stem))));

            // Toggle ge- on the correct participle (add or remove).
            AddCandidate(candidates, ChangeParticipleGe(shape, correctAnswer));

            // Ending swap on the correct participle: -en <-> -t
            AddCandidate(candidates, ChangeEnding(correctAnswer, IrregularVerbForm.Perfect));

            // Ablaut / umlaut variants of the correct participle stem.
            AddCandidate(candidates, RemoveUmlauts(shape, correctAnswer, IrregularVerbForm.Perfect));
            foreach (var vowelCandidate in CreateVowelCandidates(
                shape,
                correctAnswer,
                IrregularVerbForm.Perfect))
            {
                AddCandidate(candidates, vowelCandidate);
            }

            // Pattern: ge + past-stem + en  (mix past ablaut into participle)
            if (!string.IsNullOrWhiteSpace(word.Past))
            {
                var pastStem = ExtractMutableStem(
                    GetPrimaryToken(word.Past!, isLast: false),
                    shape,
                    IrregularVerbForm.Prateritum);
                if (!string.IsNullOrEmpty(pastStem))
                {
                    AddCandidate(
                        candidates,
                        ReplaceLastWord(
                            correctAnswer,
                            AssembleParticiple(shape, pastStem, withGe: true, "en")));
                    AddCandidate(
                        candidates,
                        ReplaceLastWord(
                            correctAnswer,
                            AssembleParticiple(shape, pastStem, withGe: false, "en")));
                }
            }

            // Infinitive itself (= root+en / full verb) as a common strong-verb trap.
            AddCandidate(candidates, ReplaceLastWord(correctAnswer, shape.Infinitive));
            AddCandidate(candidates, word.Word);
        }

        private static VerbShape Analyze(WordEntry word)
        {
            var infinitive = GetInfinitive(word.Word);
            var storedPrefix = word.Prefix?.Trim() ?? string.Empty;

            if (word.IsSeparable &&
                storedPrefix.Length > 0 &&
                infinitive.StartsWith(storedPrefix, StringComparison.OrdinalIgnoreCase) &&
                infinitive.Length > storedPrefix.Length)
            {
                var separable = infinitive[..storedPrefix.Length];
                var root = infinitive[separable.Length..];
                return new VerbShape(
                    Infinitive: infinitive,
                    Root: root,
                    Stem: GetStem(root),
                    SeparablePrefix: separable,
                    InseparablePrefix: string.Empty,
                    IsSeparable: true);
            }

            if (!word.IsSeparable &&
                storedPrefix.Length > 0 &&
                infinitive.StartsWith(storedPrefix, StringComparison.OrdinalIgnoreCase) &&
                infinitive.Length > storedPrefix.Length)
            {
                var inseparable = infinitive[..storedPrefix.Length];
                return new VerbShape(
                    Infinitive: infinitive,
                    Root: infinitive,
                    Stem: GetStem(infinitive),
                    SeparablePrefix: string.Empty,
                    InseparablePrefix: inseparable,
                    IsSeparable: false);
            }

            return new VerbShape(
                Infinitive: infinitive,
                Root: infinitive,
                Stem: GetStem(infinitive),
                SeparablePrefix: string.Empty,
                InseparablePrefix: string.Empty,
                IsSeparable: false);
        }

        /// <summary>
        /// Builds a participle from the strong/weak shells:
        /// sep+ge+STEM+ending | ge+STEM+ending | insepSTEM+ending | STEM+ending.
        /// </summary>
        private static string AssembleParticiple(
            VerbShape shape,
            string stem,
            bool withGe,
            string ending)
        {
            if (!string.IsNullOrEmpty(shape.SeparablePrefix))
            {
                return withGe
                    ? shape.SeparablePrefix + "ge" + stem + ending
                    : shape.SeparablePrefix + stem + ending;
            }

            if (!string.IsNullOrEmpty(shape.InseparablePrefix))
            {
                // Inseparable verbs never take ge- after the prefix; stem already includes it.
                return stem + ending;
            }

            return withGe ? "ge" + stem + ending : stem + ending;
        }

        private static string BuildPrateritumPhrase(
            string finiteVerb,
            string correctAnswer,
            VerbShape shape)
        {
            var parts = correctAnswer.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1)
            {
                parts[0] = MatchCapitalization(parts[0], finiteVerb);
                return string.Join(' ', parts);
            }

            if (!string.IsNullOrEmpty(shape.SeparablePrefix))
            {
                return finiteVerb + " " + shape.SeparablePrefix;
            }

            return finiteVerb;
        }

        private static string ChangeParticipleGe(VerbShape shape, string answer)
        {
            var words = answer.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                return string.Empty;
            }

            var index = words.Length - 1;
            var word = words[index];
            var geIndex = 0;

            if (!string.IsNullOrEmpty(shape.SeparablePrefix) &&
                word.StartsWith(shape.SeparablePrefix, StringComparison.OrdinalIgnoreCase))
            {
                geIndex = shape.SeparablePrefix.Length;
            }
            else if (!string.IsNullOrEmpty(shape.InseparablePrefix) &&
                word.StartsWith(shape.InseparablePrefix, StringComparison.OrdinalIgnoreCase))
            {
                // Wrongly insert/remove ge after an inseparable prefix.
                geIndex = shape.InseparablePrefix.Length;
            }

            words[index] = word.AsSpan(geIndex).StartsWith("ge", StringComparison.OrdinalIgnoreCase)
                ? word.Remove(geIndex, 2)
                : word.Insert(geIndex, "ge");
            return string.Join(' ', words);
        }

        private static string ChangeEnding(string answer, IrregularVerbForm form)
        {
            var words = answer.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                return string.Empty;
            }

            var index = form == IrregularVerbForm.Perfect ? words.Length - 1 : 0;
            var token = words[index];
            words[index] = form switch
            {
                IrregularVerbForm.Prateritum when token.EndsWith("ete", StringComparison.OrdinalIgnoreCase) =>
                    token[..^3] + "te",
                IrregularVerbForm.Prateritum when token.EndsWith("te", StringComparison.OrdinalIgnoreCase) =>
                    token[..^2] + "ete",
                IrregularVerbForm.Prateritum => token + "te",
                _ when token.EndsWith("en", StringComparison.OrdinalIgnoreCase) =>
                    token[..^2] + "t",
                _ when token.EndsWith("et", StringComparison.OrdinalIgnoreCase) =>
                    token[..^2] + "en",
                _ when token.EndsWith('t') => token[..^1] + "en",
                _ => token + "t"
            };

            return string.Join(' ', words);
        }

        private static IEnumerable<string> CreateVowelCandidates(
            VerbShape shape,
            string answer,
            IrregularVerbForm form)
        {
            var words = answer.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                yield break;
            }

            var wordIndex = form == IrregularVerbForm.Perfect ? words.Length - 1 : 0;
            var token = words[wordIndex];
            var (stemStart, stemLength) = GetMutableStemRange(shape, token, form);
            if (stemLength <= 0)
            {
                yield break;
            }

            var stem = token.Substring(stemStart, stemLength);
            foreach (var (from, to) in VowelChanges)
            {
                var index = stem.IndexOf(from, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                {
                    continue;
                }

                var changedStem = stem.Remove(index, from.Length).Insert(index, to);
                words[wordIndex] = token
                    .Remove(stemStart, stemLength)
                    .Insert(stemStart, changedStem);
                yield return string.Join(' ', words);
                words[wordIndex] = token;
            }
        }

        private static string RemoveUmlauts(
            VerbShape shape,
            string answer,
            IrregularVerbForm form) =>
            TransformStem(
                shape,
                answer,
                form,
                value => value
                    .Replace("ä", "a", StringComparison.OrdinalIgnoreCase)
                    .Replace("ö", "o", StringComparison.OrdinalIgnoreCase)
                    .Replace("ü", "u", StringComparison.OrdinalIgnoreCase)
                    .Replace("äu", "au", StringComparison.OrdinalIgnoreCase));

        private static string TransformStem(
            VerbShape shape,
            string answer,
            IrregularVerbForm form,
            Func<string, string> transform)
        {
            var words = answer.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                return string.Empty;
            }

            var wordIndex = form == IrregularVerbForm.Perfect ? words.Length - 1 : 0;
            var token = words[wordIndex];
            var (stemStart, stemLength) = GetMutableStemRange(shape, token, form);
            if (stemLength <= 0)
            {
                return string.Empty;
            }

            var stem = token.Substring(stemStart, stemLength);
            var changedStem = transform(stem);
            if (string.Equals(stem, changedStem, StringComparison.Ordinal))
            {
                return string.Empty;
            }

            words[wordIndex] = token
                .Remove(stemStart, stemLength)
                .Insert(stemStart, changedStem);
            return string.Join(' ', words);
        }

        private static string ExtractMutableStem(
            string conjugatedWord,
            VerbShape shape,
            IrregularVerbForm form)
        {
            var (start, length) = GetMutableStemRange(shape, conjugatedWord, form);
            return length <= 0 ? string.Empty : conjugatedWord.Substring(start, length);
        }

        private static (int Start, int Length) GetMutableStemRange(
            VerbShape shape,
            string conjugatedWord,
            IrregularVerbForm form)
        {
            var start = 0;

            if (form == IrregularVerbForm.Perfect)
            {
                if (!string.IsNullOrEmpty(shape.SeparablePrefix) &&
                    conjugatedWord.StartsWith(
                        shape.SeparablePrefix,
                        StringComparison.OrdinalIgnoreCase))
                {
                    start = shape.SeparablePrefix.Length;
                    if (conjugatedWord.AsSpan(start).StartsWith(
                        "ge",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        start += 2;
                    }
                }
                else if (!string.IsNullOrEmpty(shape.InseparablePrefix) &&
                    conjugatedWord.StartsWith(
                        shape.InseparablePrefix,
                        StringComparison.OrdinalIgnoreCase))
                {
                    start = shape.InseparablePrefix.Length;
                }
                else if (conjugatedWord.StartsWith("ge", StringComparison.OrdinalIgnoreCase))
                {
                    start = 2;
                }
            }
            else if (!string.IsNullOrEmpty(shape.InseparablePrefix) &&
                conjugatedWord.StartsWith(
                    shape.InseparablePrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                start = shape.InseparablePrefix.Length;
            }

            var end = conjugatedWord.Length;
            var mutablePart = conjugatedWord[start..];
            if (form == IrregularVerbForm.Perfect)
            {
                if (mutablePart.EndsWith("en", StringComparison.OrdinalIgnoreCase))
                {
                    end -= 2;
                }
                else if (mutablePart.EndsWith("et", StringComparison.OrdinalIgnoreCase))
                {
                    end -= 2;
                }
                else if (mutablePart.EndsWith('t'))
                {
                    end--;
                }
            }
            else if (mutablePart.EndsWith("ete", StringComparison.OrdinalIgnoreCase))
            {
                end -= 3;
            }
            else if (mutablePart.EndsWith("te", StringComparison.OrdinalIgnoreCase))
            {
                end -= 2;
            }
            else if (mutablePart.EndsWith('e'))
            {
                end--;
            }

            return (start, Math.Max(0, end - start));
        }

        private static string GetInfinitive(string phrase) =>
            phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? phrase;

        private static string GetPrimaryToken(string phrase, bool isLast)
        {
            var parts = phrase
                .Split(['/', ';', '|'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault() ?? phrase;
            var words = parts.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                return parts;
            }

            return isLast ? words[^1] : words[0];
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

        private static string ReplaceLastWord(string phrase, string replacement)
        {
            var words = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                return replacement;
            }

            words[^1] = MatchCapitalization(words[^1], replacement);
            return string.Join(' ', words);
        }

        private static string MatchCapitalization(string original, string replacement) =>
            original.Length > 0 && char.IsUpper(original[0]) && replacement.Length > 0
                ? char.ToUpperInvariant(replacement[0]) + replacement[1..]
                : replacement;

        private static void AddCandidate(ICollection<string> candidates, string candidate)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                candidates.Add(candidate.Trim());
            }
        }

        private sealed record VerbShape(
            string Infinitive,
            string Root,
            string Stem,
            string SeparablePrefix,
            string InseparablePrefix,
            bool IsSeparable);
    }
}
