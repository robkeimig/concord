namespace Concord.WinForms;

public class Server
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; }
    public string IpAddress { get; set; }
    public byte[] LogoAsset { get; set; }
    public string LogoAssetContentType { get; set; }
    public string? AccessToken { get; set; }
}
