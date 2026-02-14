namespace Concord.Data;

public enum ChannelType
{
    Text = 0,
    Voice = 1,
}

public class Channel
{
    public long Id;
    public string DisplayName;
    public ChannelType Type;
    public DateTime CreatedUtc;
}

public class ChannelsTable
{
    public const string TableName = "Channels";

    public long Id { get; set; }
    public string DisplayName { get; set; }
    public int Type { get; set; }
    public long CreatedUnixTimestamp { get; set; }
}

public static class ChannelDataExtensions
{
    static Channel Map(ChannelsTable row) => new Channel
    {
        Id = row.Id,
        DisplayName = row.DisplayName,
        Type = (ChannelType)row.Type,
        CreatedUtc = DateTimeOffset.FromUnixTimeSeconds(row.CreatedUnixTimestamp).UtcDateTime,
    };

    static ChannelsTable Map(Channel channel) => new ChannelsTable
    {
        Id = channel.Id,
        DisplayName = channel.DisplayName,
        Type = (int)channel.Type,
        CreatedUnixTimestamp = new DateTimeOffset(channel.CreatedUtc).ToUnixTimeSeconds(),
    };
}
