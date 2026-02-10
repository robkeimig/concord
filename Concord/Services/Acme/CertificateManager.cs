using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;

namespace Concord.Services.Acme;

public interface ICertificateManager
{
    Task<X509Certificate2> GetOrRenewAsync(CancellationToken ct);
}

public sealed class CertificateManager(
    IPublicIpService ipService,
    IAcmeClient acme,
    ICertificateStore store,
    ICurrentCertificateProvider current,
    ILogger<CertificateManager> logger
) : ICertificateManager
{
    public async Task<X509Certificate2> GetOrRenewAsync(CancellationToken ct)
    {
        var ip = await ipService.GetPublicIpAsync(ct);
        var cert = await store.LoadAsync(ip, ct);
        var now = DateTimeOffset.UtcNow;
        var needsRenew = cert is null || (now - cert.NotBefore) > TimeSpan.FromHours(24) || (cert.NotAfter - now) < TimeSpan.FromDays(1);
        if (!needsRenew && cert is not null)
        {
            current.Current = cert;
            return cert;
        }

        logger.LogInformation("Requesting Let's Encrypt IP certificate for {Ip}", ip);
        var newCert = await acme.EnsureIpCertificateAsync(ip, ct);
        await store.SaveAsync(ip, newCert, ct);
        current.Current = newCert;
        return newCert;
    }
}

public sealed class CertificateRenewalService(
    ICertificateManager manager,
    ILogger<CertificateRenewalService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await manager.GetOrRenewAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Certificate renewal failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
            catch (TaskCanceledException) { }
        }
    }
}
