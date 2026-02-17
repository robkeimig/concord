namespace WinFormsClient;

internal static class ServerUri
{
    public static string GetScheme(string ipAddress)
        => string.Equals(ipAddress, "127.0.0.1", StringComparison.Ordinal) ? "http" : "https";

    public static Uri GetBaseUri(string ipAddress)
        => new($"{GetScheme(ipAddress)}://{ipAddress}");

    public static Uri GetUri(string ipAddress, string relativePath)
    {
        if (relativePath is null)
            throw new ArgumentNullException(nameof(relativePath));

        relativePath = relativePath.TrimStart('/');
        return new Uri(GetBaseUri(ipAddress), relativePath);
    }
}
