using SQLite;

namespace GermanToolbox
{
    [Table("Words")]
    public sealed class WordEntry
    {
        [PrimaryKey]
        public int Id { get; set; }

        [Indexed]
        public string Word { get; set; } = string.Empty;

        [Indexed]
        public string Translation { get; set; } = string.Empty;

        [Indexed]
        public string Type { get; set; } = string.Empty;

        public string? Gender { get; set; }

        public string? Plural { get; set; }

        [Indexed]
        public string Level { get; set; } = string.Empty;

        [Indexed]
        public bool IsStrong { get; set; }

        public string? Past { get; set; }

        public string? Perfekt { get; set; }

        public bool Learning { get; set; }

        public int ScoreMeaning { get; set; }

        public int ScoreReverseMeaning { get; set; }

        public int ScoreArticle { get; set; }

        public int ScorePlural { get; set; }

        public int ScoreIrregularPrateritum { get; set; }

        public int ScoreIrregularPerfect { get; set; }

        public bool MistakeMeaning { get; set; }

        public bool MistakeArticle { get; set; }

        public bool MistakePlural { get; set; }

        public bool MistakeIrregular { get; set; }
    }
}
