using System.Security.Cryptography.X509Certificates;

namespace Concord.Services.Acme;

public interface ICertificateStore
{
    Task<X509Certificate2?> LoadAsync(string ip, CancellationToken ct);
    Task SaveAsync(string ip, X509Certificate2 cert, CancellationToken ct);
}

public sealed class FileCertificateStore(string dirPath) : ICertificateStore
{
    private readonly string _dir = dirPath;

    public async Task<X509Certificate2?> LoadAsync(string ip, CancellationToken ct)
    {
        var path = Path.Combine(_dir, Sanitize(ip) + ".pfx");
        if (!File.Exists(path)) return null;
        var raw = await File.ReadAllBytesAsync(path, ct);
#pragma warning disable SYSLIB0057
        return new X509Certificate2(raw, (string?)null, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet);
#pragma warning restore SYSLIB0057
    }

    public async Task SaveAsync(string ip, X509Certificate2 cert, CancellationToken ct)
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, Sanitize(ip) + ".pfx");
        var raw = cert.Export(X509ContentType.Pfx);
        await File.WriteAllBytesAsync(path, raw, ct);
    }

    private static string Sanitize(string s) => s.Replace(':', '_').Replace('/', '_');
}
