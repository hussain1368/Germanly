using SQLite;

namespace GermanToolbox
{
    [Table("SeedRuns")]
    public sealed class SeedRun
    {
        [PrimaryKey]
        public string Key { get; set; } = string.Empty;

        public DateTime RanAtUtc { get; set; }
    }
}
