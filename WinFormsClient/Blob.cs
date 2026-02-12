namespace WinFormsClient;

public class Blob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ContentType { get; set; }
    public byte[] Data { get; set; }
}
