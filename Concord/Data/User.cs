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
