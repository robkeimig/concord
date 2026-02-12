using System.Security.Cryptography;

namespace Concord.Services.Acme;

public interface IAcmeAccountStore
{
    Task<(ECDsa key, string kid)?> LoadAsync(CancellationToken ct);
    Task SaveAsync(ECDsa key, string kid, CancellationToken ct);
}

public sealed class FileAcmeAccountStore(string dirPath) : IAcmeAccountStore
{
    private readonly string _keyPath = Path.Combine(dirPath, "acme-account-key.pem");
    private readonly string _kidPath = Path.Combine(dirPath, "acme-account-kid.txt");

    public async Task<(ECDsa key, string kid)?> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(_keyPath) || !File.Exists(_kidPath)) return null;
        var keyPem = await File.ReadAllTextAsync(_keyPath, ct);
        var kid = await File.ReadAllTextAsync(_kidPath, ct);
        var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(keyPem);
        return (ecdsa, kid.Trim());
    }

    public async Task SaveAsync(ECDsa key, string kid, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_keyPath)!);
        var pem = key.ExportECPrivateKeyPem();
        await File.WriteAllTextAsync(_keyPath, pem, ct);
        await File.WriteAllTextAsync(_kidPath, kid, ct);
    }
}
