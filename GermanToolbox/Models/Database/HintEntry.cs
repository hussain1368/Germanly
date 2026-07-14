using SQLite;

namespace GermanToolbox
{
    [Table("Hints")]
    public sealed class HintEntry
    {
        [PrimaryKey]
        public int Id { get; set; }

        public string? Gender { get; set; }

        public string Rule { get; set; } = string.Empty;

        public string? Suffix { get; set; }

        public int Percentage { get; set; }
    }
}
