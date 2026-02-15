namespace Concord.Services;

public static class PublicIpService
{
    const string CheckIpUrl = "https://checkip.amazonaws.com/";
    private static readonly HttpClient _httpClient = new();
    private static readonly Uri CheckIpUri = new(CheckIpUrl);

    public static async Task<string> GetPublicIpAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(
            CheckIpUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var ip = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();

        if (string.IsNullOrWhiteSpace(ip))
            throw new InvalidOperationException("Amazon IP checker returned empty response.");

        return ip;
    }
}