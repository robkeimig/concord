using Dapper;
using Microsoft.Data.Sqlite;

namespace Concord.Data
{
    public class Database
    {
        public const string Name = "concord.db";
        public const int Version = 1;

        public Database()
        {
            using var sql = new SqliteConnection(ConnectionString);
            var version = sql.ExecuteScalar<long>("PRAGMA user_version");

            for (long x = version; x < Version; x++)
            {
                Migrate(sql, x);
            }
        }



        string ConnectionString => new SqliteConnectionStringBuilder()
        {
            DataSource = Name,
            Pooling = true,
        }.ConnectionString;

        private static void Migrate(SqliteConnection sql, long x)
        {
            switch (x)
            {
                case 0:
                    MigrateFrom0(sql);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        private static void MigrateFrom0(SqliteConnection sql)
        {
            sql.Execute($@"
PRAGMA journal_mode = WAL;
PRAGMA user_version = 1;

CREATE TABLE {UsersTable.TableName} (
    [{nameof(UsersTable.Id)}] INTEGER PRIMARY KEY,
    [{nameof(UsersTable.DisplayName)}] TEXT,
    [{nameof(UsersTable.CreatedUnixTimestamp)}] INTEGER,
    [{nameof(UsersTable.AccessedUnixTimestamp)}] INTEGER,
    [{nameof(UsersTable.PrimaryColor)}] TEXT,
    [{nameof(UsersTable.AccessToken)}] BINARY);

CREATE TABLE {MessagesTable.TableName} (
);
");
        }
    }
}
