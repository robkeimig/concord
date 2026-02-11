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

        // On Windows, certificates created from PEM + CopyWithPrivateKey can hold an ephemeral key
        // that cannot be exported as PKCS#12 (PFX). Re-importing into a new instance persists the key
        // and makes it exportable.
        //
        // NOTE: We keep it passwordless since this store is file-backed and access-controlled by the host.
#pragma warning disable SYSLIB0057
        using var persisted = new X509Certificate2(
            cert.Export(X509ContentType.Pkcs12),
            (string?)null,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet);
#pragma warning restore SYSLIB0057

        var raw = persisted.Export(X509ContentType.Pfx);
        await File.WriteAllBytesAsync(path, raw, ct);
    }

    private static string Sanitize(string s) => s.Replace(':', '_').Replace('/', '_');
}
