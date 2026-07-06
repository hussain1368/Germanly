namespace GermanToolbox
{
    public readonly record struct ArticleForms(string Masculine, string Feminine, string Neuter)
    {
        public string Summary => $"{Masculine}, {Feminine}, {Neuter}";

        public string ForGender(string? gender) =>
            gender switch
            {
                "m" => Masculine,
                "f" => Feminine,
                "n" => Neuter,
                _ => string.Empty
            };
    }
}
