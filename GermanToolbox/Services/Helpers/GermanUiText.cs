namespace GermanToolbox
{
    public static class GermanUiText
    {
        public static string FormatArticleCase(ArticleCase articleCase) =>
            articleCase switch
            {
                ArticleCase.Accusative => "Akkusativ",
                ArticleCase.Dative => "Dativ",
                ArticleCase.Genitive => "Genitiv",
                _ => "Nominativ"
            };
    }
}
