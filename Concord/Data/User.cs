using Dapper;
using Microsoft.Data.Sqlite;

namespace Concord.Data;

public class User
{
    public long Id;
    public string DisplayName;
    public DateTime CreatedUtc;
    public DateTime AccessedUtc;
    public string PrimaryColor;
    public byte[] AccessToken;
}

public class UsersTable
{
    public const string TableName = "Users";
    public long Id { get; set; }
    public string DisplayName { get; set; }
    public long CreatedUnixTimestamp { get; set; }
    public long AccessedUnixTimestamp { get; set; }
    public string PrimaryColor { get; set; }
    public byte[] AccessToken { get; set; }
}

public static class UserDataExtensions
{
    public static void BootstrapUsers(this SqliteConnection sql)
    {
        var userCount = sql.ExecuteScalar<long>($"SELECT COUNT(1) FROM {UsersTable.TableName};");
        if (userCount > 0)
            return;

        var existingInvitationCode = sql.ExecuteScalar<string?>(
            $"SELECT [{nameof(InvitationsTable.Code)}] FROM {InvitationsTable.TableName} ORDER BY [{nameof(InvitationsTable.CreatedUnixTimestamp)}] DESC LIMIT 1;");

        if (!string.IsNullOrWhiteSpace(existingInvitationCode))
        {
            Console.WriteLine(existingInvitationCode);
            return;
        }

        var code = Guid.NewGuid().ToString("n");

        sql.Execute($@"
INSERT INTO {InvitationsTable.TableName}
    ([{nameof(InvitationsTable.Code)}], [{nameof(InvitationsTable.CreatedUnixTimestamp)}], [{nameof(InvitationsTable.IsPermanent)}])
VALUES
    (@Code, @CreatedUnixTimestamp, @IsPermanent);",
            new
            {
                Code = code,
                CreatedUnixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                IsPermanent = 1
            });

        Console.WriteLine(code);
    }

    static User Map(UsersTable row) => new User
    {
        Id = row.Id,
        CreatedUtc = DateTimeOffset.FromUnixTimeSeconds(row.CreatedUnixTimestamp).UtcDateTime,
        AccessedUtc = DateTimeOffset.FromUnixTimeSeconds(row.CreatedUnixTimestamp).UtcDateTime,
        PrimaryColor = row.PrimaryColor,    
        DisplayName = row.DisplayName,
    };

    static UsersTable Map(User user) => new UsersTable
    {
        Id = user.Id,
        CreatedUnixTimestamp = new DateTimeOffset(user.CreatedUtc).ToUnixTimeSeconds(),
        AccessedUnixTimestamp = new DateTimeOffset(user.AccessedUtc).ToUnixTimeSeconds(),
        PrimaryColor = user.PrimaryColor,
        DisplayName = user.DisplayName
    };
}
