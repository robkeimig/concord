namespace WinFormsClient;

public class Server
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; }
    public string IpAddress { get; set; }
    public Guid? LogoAssetBlobId { get; set; }
}
