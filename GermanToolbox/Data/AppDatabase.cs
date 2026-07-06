using SQLite;

namespace GermanToolbox
{
    public sealed class AppDatabase
    {
        public AppDatabase()
        {
            SQLitePCL.Batteries_V2.Init();
            Connection = new SQLiteAsyncConnection(DatabaseConstants.DatabasePath, DatabaseConstants.Flags);
        }

        public SQLiteAsyncConnection Connection { get; }
    }
}
