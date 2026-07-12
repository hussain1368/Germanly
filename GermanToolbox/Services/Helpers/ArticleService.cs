namespace GermanToolbox
{
    public static class ArticleService
    {
        public static ArticleForms GetForms(ArticleCase articleCase, ArticleType articleType) =>
            (articleCase, articleType) switch
            {
                (ArticleCase.Nominative, ArticleType.Definite) => new("der", "die", "das"),
                (ArticleCase.Accusative, ArticleType.Definite) => new("den", "die", "das"),
                (ArticleCase.Dative, ArticleType.Definite) => new("dem", "der", "dem"),
                (ArticleCase.Genitive, ArticleType.Definite) => new("des", "der", "des"),
                (ArticleCase.Nominative, ArticleType.Indefinite) => new("ein", "eine", "ein"),
                (ArticleCase.Accusative, ArticleType.Indefinite) => new("einen", "eine", "ein"),
                (ArticleCase.Dative, ArticleType.Indefinite) => new("einem", "einer", "einem"),
                (ArticleCase.Genitive, ArticleType.Indefinite) => new("eines", "einer", "eines"),
                _ => new("der", "die", "das")
            };

        public static string GetExpectedArticle(
            string? gender,
            ArticleCase articleCase,
            ArticleType articleType) =>
            GetForms(articleCase, articleType).ForGender(gender);
    }
}
