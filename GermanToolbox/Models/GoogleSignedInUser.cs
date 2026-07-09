namespace GermanToolbox
{
    public sealed record GoogleSignedInUser(
        string Email,
        string DisplayName,
        string FirstName,
    string? PhotoPath);
}