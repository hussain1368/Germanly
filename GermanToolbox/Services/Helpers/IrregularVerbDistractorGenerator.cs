namespace GermanToolbox
{
    public sealed class IrregularVerbDistractorGenerator
    {
        private static readonly string[] SeparablePrefixes =
        [
            "ab", "an", "auf", "aus", "bei", "ein", "entgegen", "entlang", "fest",
            "fort", "her", "heraus", "herunter", "hin", "hoch", "los", "mit", "nach",
            "statt", "teil", "umher", "vor", "vorbei", "weg", "weiter", "zu", "zurück"
        ];

        private static readonly string[] InseparablePrefixes =
        [
            "be", "emp", "ent", "er", "ge", "miss", "ver", "zer"
        ];

        private static readonly (string From, string To)[] VowelChanges =
        [
            ("ä", "a"),
            ("ö", "o"),
            ("ü", "u"),
            ("ie", "ei"),
            ("ie", "i"),
            ("ei", "ie"),
            ("a", "ä"),
            ("e", "i"),
            ("e", "ie"),
            ("i", "a"),
            ("i", "o"),
            ("o", "a"),
            ("u", "a")
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
            var primaryAnswer = IrregularVerbAnswerService.GetAcceptedAnswers(word, form).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(primaryAnswer) || maximumCount <= 0)
            {
                return [];
            }

            var candidates = new List<string>();
            AddCandidate(candidates, CreateRegularizedForm(word.Word, primaryAnswer, form));
            AddCandidate(candidates, ChangeEnding(primaryAnswer, form));

            AddCandidate(candidates, RemoveUmlauts(word.Word, primaryAnswer, form));
            foreach (var vowelCandidate in CreateVowelCandidates(word.Word, primaryAnswer, form))
            {
                AddCandidate(candidates, vowelCandidate);
            }

            AddCandidate(candidates, CreateRegularizedWithoutGe(word.Word, primaryAnswer, form));
            AddCandidate(candidates, ChangeParticiplePrefix(word.Word, primaryAnswer, form));
            AddCandidate(candidates, CreateAlternativeRegularEnding(word.Word, primaryAnswer, form));
            AddCandidate(candidates, word.Word);

            var uniqueCandidates = candidates
                .Where(candidate => !correctAnswers.Contains(
                    IrregularVerbAnswerService.Normalize(candidate)))
                .DistinctBy(
                    IrregularVerbAnswerService.Normalize,
                    StringComparer.OrdinalIgnoreCase)
                .Take(maximumCount)
                .ToList();

            return uniqueCandidates;
        }

        private static string CreateRegularizedForm(
            string infinitivePhrase,
            string correctAnswer,
            IrregularVerbForm form)
        {
            var infinitive = GetInfinitive(infinitivePhrase);
            var (prefix, root) = SplitSeparablePrefix(infinitive);
            var stem = GetStem(root);

            return form switch
            {
                IrregularVerbForm.Prateritum =>
                    ReplaceFirstWord(correctAnswer, stem + GetPastEnding(stem)),
                _ => ReplaceLastWord(
                    correctAnswer,
                    CreateRegularParticiple(infinitive, prefix, root, stem))
            };
        }

        private static string CreateAlternativeRegularEnding(
            string infinitivePhrase,
            string correctAnswer,
            IrregularVerbForm form)
        {
            var infinitive = GetInfinitive(infinitivePhrase);
            var (prefix, root) = SplitSeparablePrefix(infinitive);
            var stem = GetStem(root);

            return form switch
            {
                IrregularVerbForm.Prateritum => ReplaceFirstWord(correctAnswer, stem + "ete"),
                _ => ReplaceLastWord(
                    correctAnswer,
                    CreateParticiple(infinitive, prefix, stem, "en"))
            };
        }

        private static string CreateRegularizedWithoutGe(
            string infinitivePhrase,
            string correctAnswer,
            IrregularVerbForm form)
        {
            if (form != IrregularVerbForm.Perfect)
            {
                return string.Empty;
            }

            var infinitive = GetInfinitive(infinitivePhrase);
            var (prefix, root) = SplitSeparablePrefix(infinitive);
            var stem = GetStem(root);
            return ReplaceLastWord(
                correctAnswer,
                prefix + stem + GetPresentEnding(stem));
        }

        private static string ChangeEnding(string answer, IrregularVerbForm form)
        {
            var words = answer.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                return string.Empty;
            }

            var index = form == IrregularVerbForm.Perfect ? words.Length - 1 : 0;
            var word = words[index];
            words[index] = form switch
            {
                IrregularVerbForm.Prateritum when word.EndsWith("te", StringComparison.OrdinalIgnoreCase) =>
                    word[..^2] + "ete",
                IrregularVerbForm.Prateritum => word + "te",
                _ when word.EndsWith("en", StringComparison.OrdinalIgnoreCase) =>
                    word[..^2] + "t",
                _ when word.EndsWith('t') => word[..^1] + "en",
                _ => word + "t"
            };

            return string.Join(' ', words);
        }

        private static string ChangeParticiplePrefix(
            string infinitivePhrase,
            string answer,
            IrregularVerbForm form)
        {
            if (form != IrregularVerbForm.Perfect)
            {
                return string.Empty;
            }

            var words = answer.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                return string.Empty;
            }

            var index = words.Length - 1;
            var word = words[index];
            var infinitive = GetInfinitive(infinitivePhrase);
            var (separablePrefix, _) = SplitSeparablePrefix(infinitive);
            var inseparablePrefix = GetInseparablePrefix(infinitive);
            var geIndex = 0;

            if (!string.IsNullOrEmpty(separablePrefix) &&
                word.StartsWith(separablePrefix, StringComparison.OrdinalIgnoreCase))
            {
                geIndex = separablePrefix.Length;
            }
            else if (!string.IsNullOrEmpty(inseparablePrefix) &&
                word.StartsWith(inseparablePrefix, StringComparison.OrdinalIgnoreCase))
            {
                geIndex = inseparablePrefix.Length;
            }

            words[index] = word.AsSpan(geIndex).StartsWith(
                "ge",
                StringComparison.OrdinalIgnoreCase)
                    ? word.Remove(geIndex, 2)
                    : word.Insert(geIndex, "ge");
            return string.Join(' ', words);
        }

        private static IEnumerable<string> CreateVowelCandidates(
            string infinitivePhrase,
            string answer,
            IrregularVerbForm form)
        {
            var words = answer.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                yield break;
            }

            var wordIndex = form == IrregularVerbForm.Perfect ? words.Length - 1 : 0;
            var word = words[wordIndex];
            var (stemStart, stemLength) = GetMutableStemRange(
                GetInfinitive(infinitivePhrase),
                word,
                form);
            if (stemLength <= 0)
            {
                yield break;
            }

            var stem = word.Substring(stemStart, stemLength);
            foreach (var (from, to) in VowelChanges)
            {
                var index = stem.IndexOf(from, StringComparison.OrdinalIgnoreCase);
                if (index >= 0)
                {
                    var changedStem = stem.Remove(index, from.Length).Insert(index, to);
                    words[wordIndex] = word
                        .Remove(stemStart, stemLength)
                        .Insert(stemStart, changedStem);
                    yield return string.Join(' ', words);
                    words[wordIndex] = word;
                }
            }
        }

        private static string RemoveUmlauts(
            string infinitivePhrase,
            string answer,
            IrregularVerbForm form) =>
            TransformStem(
                infinitivePhrase,
                answer,
                form,
                value => value
                .Replace("ä", "a", StringComparison.OrdinalIgnoreCase)
                .Replace("ö", "o", StringComparison.OrdinalIgnoreCase)
                .Replace("ü", "u", StringComparison.OrdinalIgnoreCase));

        private static string CreateRegularParticiple(
            string infinitive,
            string prefix,
            string root,
            string stem)
        {
            return CreateParticiple(
                infinitive,
                prefix,
                stem,
                GetPresentEnding(stem));
        }

        private static string CreateParticiple(
            string infinitive,
            string separablePrefix,
            string stem,
            string ending)
        {
            if (!string.IsNullOrEmpty(separablePrefix))
            {
                return separablePrefix + "ge" + stem + ending;
            }

            if (infinitive.EndsWith("ieren", StringComparison.OrdinalIgnoreCase) ||
                !string.IsNullOrEmpty(GetInseparablePrefix(infinitive)))
            {
                return stem + ending;
            }

            return "ge" + stem + ending;
        }

        private static string GetInfinitive(string phrase) =>
            phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? phrase;

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

        private static string GetInseparablePrefix(string infinitive) =>
            InseparablePrefixes
                .Where(item => infinitive.StartsWith(item, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.Length)
                .FirstOrDefault(item => infinitive.Length > item.Length + 2) ??
            string.Empty;

        private static string TransformStem(
            string infinitivePhrase,
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
            var word = words[wordIndex];
            var (stemStart, stemLength) = GetMutableStemRange(
                GetInfinitive(infinitivePhrase),
                word,
                form);
            if (stemLength <= 0)
            {
                return string.Empty;
            }

            var stem = word.Substring(stemStart, stemLength);
            var changedStem = transform(stem);
            if (string.Equals(stem, changedStem, StringComparison.Ordinal))
            {
                return string.Empty;
            }

            words[wordIndex] = word
                .Remove(stemStart, stemLength)
                .Insert(stemStart, changedStem);
            return string.Join(' ', words);
        }

        private static (int Start, int Length) GetMutableStemRange(
            string infinitive,
            string conjugatedWord,
            IrregularVerbForm form)
        {
            var (separablePrefix, _) = SplitSeparablePrefix(infinitive);
            var inseparablePrefix = GetInseparablePrefix(infinitive);
            var start = 0;

            if (form == IrregularVerbForm.Perfect)
            {
                if (!string.IsNullOrEmpty(separablePrefix) &&
                    conjugatedWord.StartsWith(
                        separablePrefix,
                        StringComparison.OrdinalIgnoreCase))
                {
                    start = separablePrefix.Length;
                    if (conjugatedWord.AsSpan(start).StartsWith(
                        "ge",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        start += 2;
                    }
                }
                else if (!string.IsNullOrEmpty(inseparablePrefix) &&
                    conjugatedWord.StartsWith(
                        inseparablePrefix,
                        StringComparison.OrdinalIgnoreCase))
                {
                    start = inseparablePrefix.Length;
                }
                else if (conjugatedWord.StartsWith(
                    "ge",
                    StringComparison.OrdinalIgnoreCase))
                {
                    start = 2;
                }
            }
            else if (!string.IsNullOrEmpty(inseparablePrefix) &&
                conjugatedWord.StartsWith(
                    inseparablePrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                start = inseparablePrefix.Length;
            }

            var end = conjugatedWord.Length;
            var mutablePart = conjugatedWord[start..];
            if (form == IrregularVerbForm.Perfect)
            {
                if (mutablePart.EndsWith("en", StringComparison.OrdinalIgnoreCase))
                {
                    end -= 2;
                }
                else if (mutablePart.EndsWith('t'))
                {
                    end--;
                }
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

        private static bool NeedsConnectingE(string stem) =>
            stem.EndsWith('d') ||
            stem.EndsWith('t') ||
            stem.EndsWith("chn", StringComparison.OrdinalIgnoreCase) ||
            stem.EndsWith("ffn", StringComparison.OrdinalIgnoreCase);

        private static string ReplaceFirstWord(string phrase, string replacement)
        {
            var words = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                return replacement;
            }

            words[0] = MatchCapitalization(words[0], replacement);
            return string.Join(' ', words);
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
    }
}
