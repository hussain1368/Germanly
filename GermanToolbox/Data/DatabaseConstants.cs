using Microsoft.Maui.Storage;
using SQLite;

namespace GermanToolbox
{
    public static class DatabaseConstants
    {
        public const string DatabaseFilename = "german_toolbox.db3";

        public const SQLiteOpenFlags Flags =
            SQLiteOpenFlags.ReadWrite |
            SQLiteOpenFlags.Create |
            SQLiteOpenFlags.SharedCache;

        public static string DatabasePath =>
            Path.Combine(FileSystem.AppDataDirectory, DatabaseFilename);
    }
}
