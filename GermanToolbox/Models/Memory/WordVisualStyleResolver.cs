namespace GermanToolbox
{
    public static class WordTypeClassifier
    {
        public static bool IsVerb(string? type) =>
            string.Equals(type?.Trim(), "verb", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                type?.Trim(),
                "reflexive verb",
                StringComparison.OrdinalIgnoreCase);
    }

    public static class WordVisualStyleResolver
    {
        private static readonly WordVisualStyle DefaultStyle = new(
            Color.FromArgb("#555555"),
            Color.FromArgb("#FFFFFF"),
            Color.FromArgb("#E4E8F4"));

        private static readonly WordVisualStyle MasculineStyle = new(
            Color.FromArgb("#2563EB"),
            Color.FromArgb("#F7FAFF"),
            Color.FromArgb("#CFE0FF"));

        private static readonly WordVisualStyle FeminineStyle = new(
            Color.FromArgb("#D43D35"),
            Color.FromArgb("#FFF9F8"),
            Color.FromArgb("#F2C5C2"));

        private static readonly WordVisualStyle NeuterStyle = new(
            Color.FromArgb("#2E7D32"),
            Color.FromArgb("#F8FCF8"),
            Color.FromArgb("#CBE4CC"));

        private static readonly WordVisualStyle VerbStyle = new(
            Color.FromArgb("#D97706"),
            Color.FromArgb("#FFF8EB"),
            Color.FromArgb("#F0C98F"));

        public static WordVisualStyle Resolve(WordEntry word)
        {
            if (WordTypeClassifier.IsVerb(word.Type))
            {
                return VerbStyle;
            }

            if (!string.Equals(word.Type, "noun", StringComparison.OrdinalIgnoreCase))
            {
                return DefaultStyle;
            }

            return word.Gender?.ToLowerInvariant() switch
            {
                "m" => MasculineStyle,
                "f" => FeminineStyle,
                "n" => NeuterStyle,
                _ => DefaultStyle
            };
        }
    }
}
