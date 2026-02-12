using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace Concord.Services;

public interface IPublicIpService
{
    Task<string> GetPublicIpAsync(CancellationToken cancellationToken = default);
}

public sealed class AmazonPublicIpService(HttpClient httpClient, ILogger<AmazonPublicIpService> logger) : IPublicIpService
{
    private static readonly Uri CheckIpUri = new("https://checkip.amazonaws.com/");
    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<AmazonPublicIpService> _logger = logger;

    public async Task<string> GetPublicIpAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(CheckIpUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var ip = content.Trim();
            if (string.IsNullOrWhiteSpace(ip))
            {
                throw new InvalidOperationException("Amazon IP checker returned empty response.");
            }
            return ip;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve public IP from Amazon check service");
            throw;
        }
    }
}
