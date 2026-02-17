namespace Concord.Data
{
    public class Message
    {
        public long Id;
        public long AuthorUserId;
        public long ChannelId;
        public string Content;
        public DateTime CreatedUtc;
        public DateTime? EditedUtc;
    }

    public class MessagesTable
    {
        public const string TableName = "Messages";

        public long Id { get; set; }
        public long AuthorUserId { get; set; }
        public long ChannelId { get; set; }
        public string Content { get; set; }
        public long CreatedUnixTimestamp { get; set; }
        public long? EditedUnixTimestamp { get; set; }
    }

    public static class MessageDataExtensions
    {
        static Message Map(MessagesTable row) => new Message
        {
            Id = row.Id,
            AuthorUserId = row.AuthorUserId,
            ChannelId = row.ChannelId,
            Content = row.Content,
            CreatedUtc = DateTimeOffset.FromUnixTimeSeconds(row.CreatedUnixTimestamp).UtcDateTime,
            EditedUtc = row.EditedUnixTimestamp is null
                ? null
                : DateTimeOffset.FromUnixTimeSeconds(row.EditedUnixTimestamp.Value).UtcDateTime,
        };

        static MessagesTable Map(Message message) => new MessagesTable
        {
            Id = message.Id,
            AuthorUserId = message.AuthorUserId,
            ChannelId = message.ChannelId,
            Content = message.Content,
            CreatedUnixTimestamp = new DateTimeOffset(message.CreatedUtc).ToUnixTimeSeconds(),
            EditedUnixTimestamp = message.EditedUtc is null ? null : new DateTimeOffset(message.EditedUtc.Value).ToUnixTimeSeconds(),
        };
    }
}
