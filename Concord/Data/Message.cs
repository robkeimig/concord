namespace Concord.Data
{
    public class Message
    {
        public long Id;
        //Other properties typical of a discord style text message.
    }

    public class MessagesTable
    {
        public const string TableName = "Messages";

        //...
    }

    public static class MessageDataExtesnsions
    {

    }
}
