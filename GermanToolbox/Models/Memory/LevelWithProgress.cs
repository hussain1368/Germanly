namespace GermanToolbox
{
    public sealed record LevelWithProgress(
        string Level,
        int MasteredCount,
        int TotalCount)
    {
        public int MasteredPercent =>
            TotalCount <= 0
                ? 0
                : (int)Math.Round(100d * MasteredCount / TotalCount);
    }
}
