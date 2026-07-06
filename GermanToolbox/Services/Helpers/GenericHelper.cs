namespace GermanToolbox
{
    public static class GenericHelper
    {
        public static double GetPromptFontSize(string text)
        {
            var words = text.Split(' ');
            var longestWordLength = words.Max(w => w.Length);

            if (text.Length < 10) return 56;

            else if (text.Length < 18)
            {
                if (longestWordLength < 15) return 36;
                else return 32;
            }
            else
            {
                if (longestWordLength < 18) return 32;
                if (longestWordLength < 22) return 26;
                else return 20;
            }

            /*
             * 56 - 10 Reiseurgt
             * 
             * 36 - 15 Reiseurgtykmnb
             * 32 - 17 Reiseurgtykmnbpns
             * 
             * 26 - 21 Fremdsprachenkorresow
             * 20 - 26 Fremdsprachenkorrespondent
             */
        }
    }
}
