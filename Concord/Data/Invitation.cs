using Microsoft.Data.Sqlite;

namespace Concord.Data;

public class Invitation
{
    public long Id;
    public string Code;
    public DateTime CreatedUtc;
    public bool IsPermanent;
}

public class InvitationsTable
{
    public const string TableName = "Invitations";

    public long Id { get; set; }
    public string Code { get; set; }
    public long CreatedUnixTimestamp { get; set; }
    public int IsPermanent { get; set; }
}

public static class InvitationDataExtensions
{
    static Invitation Map(InvitationsTable row) => new Invitation
    {
        Id = row.Id,
        Code = row.Code,
        CreatedUtc = DateTimeOffset.FromUnixTimeSeconds(row.CreatedUnixTimestamp).UtcDateTime,
        IsPermanent = row.IsPermanent != 0,
    };

    static InvitationsTable Map(Invitation invitation) => new InvitationsTable
    {
        Id = invitation.Id,
        Code = invitation.Code,
        CreatedUnixTimestamp = new DateTimeOffset(invitation.CreatedUtc).ToUnixTimeSeconds(),
        IsPermanent = invitation.IsPermanent ? 1 : 0,
    };
}
