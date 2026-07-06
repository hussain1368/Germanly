namespace GermanToolbox
{
    public sealed class TestSessionWord
    {
        public TestSessionWord(WordEntry word)
        {
            Word = word;
        }

        public WordEntry Word { get; }

        public int ScoreDelta { get; set; }

        public bool HasScored { get; set; }

        public bool WasMistaken { get; set; }

        public bool IsComplete { get; set; }
    }
}
