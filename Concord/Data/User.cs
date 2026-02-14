using Concord.Services;
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
    public static async Task BootstrapUsers(this SqliteConnection sql)
    {
        var userCount = sql.ExecuteScalar<long>($"SELECT COUNT(1) FROM {UsersTable.TableName};");
        if (userCount > 0)
            return;

        // Invitation creation/search lives in Concord/Data/Invitation.cs
        var existingInvitationCode = sql.GetLatestInvitationCode();
        var code = !string.IsNullOrWhiteSpace(existingInvitationCode)
            ? existingInvitationCode!
            : sql.CreatePermanentInvitation();

        var publicIpService = new AmazonPublicIpService(new HttpClient(), new LoggerFactory().CreateLogger<AmazonPublicIpService>());    
        var publicIp = await publicIpService.GetPublicIpAsync();

        Console.WriteLine();
        Console.WriteLine();
        Console.BackgroundColor = ConsoleColor.Black;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Use the following information to create the first administrator account:");
        Console.WriteLine();
        Console.WriteLine($"  Server IP:       {publicIp}");
        Console.WriteLine($"  Invitation code: {code}");
        Console.WriteLine();
        Console.WriteLine("In the desktop client, choose 'Add server' and enter the Server IP and Invitation code.");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine();
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
