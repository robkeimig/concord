using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace Concord.Services.Acme;

public interface ICurrentCertificateProvider
{
    X509Certificate2? Current { get; set; }
}

public sealed class CurrentCertificateProvider : ICurrentCertificateProvider
{
    private X509Certificate2? _current;
    public X509Certificate2? Current
    {
        get => Volatile.Read(ref _current);
        set => Volatile.Write(ref _current, value);
    }
}
