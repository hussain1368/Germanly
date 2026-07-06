namespace GermanToolbox
{
    public sealed class SearchResultItem
    {
        public SearchResultItem(WordEntry word)
        {
            var visualStyle = WordVisualStyleResolver.Resolve(word);
            Id = word.Id;
            Word = word.Word;
            Translation = word.Translation;
            Type = word.Type;
            Level = word.Level;
            AccentColor = visualStyle.AccentColor;
        }

        public int Id { get; }

        public string Word { get; }

        public string Translation { get; }

        public string Type { get; }

        public string Level { get; }

        public Color AccentColor { get; }
    }
}
