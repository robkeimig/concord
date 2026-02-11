using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Concord.Services.Acme;

public interface IAcmeClient
{
    Task<X509Certificate2> EnsureIpCertificateAsync(string ip, CancellationToken ct);
}

internal sealed class AcmeClient(
    HttpClient http,
    IAcmeHttpChallengeStore challenges,
    IAcmeAccountStore accountStore,
    ILogger<AcmeClient> logger
) : IAcmeClient
{
    private static readonly Uri DirectoryUrl = new("https://acme-v02.api.letsencrypt.org/directory");

    // LetsEncrypt requires specifying an issuance profile for IP identifier orders.
    // Allow override via env var for forward-compat.
    private static readonly string IpCertProfile = "shortlived";

    // Some ACME CAs validate that the CSR subject CN looks like an issued DNS name
    // with a valid public suffix. For IP certs, the IP must be in SAN; CN is not used.
    // Use a syntactically valid DNS name with a real public suffix by default.
    // Can be overridden via env var.
    private static readonly string CsrCommonName = "example.com";

    private record DirectoryIndex(
        [property: JsonPropertyName("newNonce")] string NewNonce,
        [property: JsonPropertyName("newAccount")] string NewAccount,
        [property: JsonPropertyName("newOrder")] string NewOrder
    );

    private record OrderPayload(
        [property: JsonPropertyName("identifiers")] Identifier[] Identifiers,
        [property: JsonPropertyName("notBefore")] string? NotBefore,
        [property: JsonPropertyName("notAfter")] string? NotAfter,
        [property: JsonPropertyName("profile")] string? Profile
    );
    private record Identifier([property: JsonPropertyName("type")] string Type, [property: JsonPropertyName("value")] string Value);

    private record Jwk(
        [property: JsonPropertyName("kty")] string Kty,
        [property: JsonPropertyName("crv")] string Crv,
        [property: JsonPropertyName("x")] string X,
        [property: JsonPropertyName("y")] string Y
    );

    private record AccountResp([property: JsonPropertyName("status")] string Status);

    private record Challenge([property: JsonPropertyName("type")] string Type, [property: JsonPropertyName("token")] string Token, [property: JsonPropertyName("url")] string Url);
    private record Authorization([property: JsonPropertyName("status")] string Status, [property: JsonPropertyName("challenges")] Challenge[] Challenges);

    private record FinalizePayload([property: JsonPropertyName("csr")] string Csr);

    private record JwsEnvelope(
        [property: JsonPropertyName("protected")] string Protected,
        [property: JsonPropertyName("payload")] string Payload,
        [property: JsonPropertyName("signature")] string Signature
    );

    private string? _nonce;

    private DirectoryIndex? _directory;

    public async Task<X509Certificate2> EnsureIpCertificateAsync(string ip, CancellationToken ct)
    {
        var dir = await GetDirectoryAsync(ct);
        using var acctKey = await GetOrCreateAccountKeyAsync(dir, ct);

        // Create order for IP identifier
        // NOTE: Let's Encrypt does not support notBefore/notAfter in newOrder.
        var order = await PostAsJwsAsync(dir.NewOrder, acctKey, kid: await GetKidAsync(ct), new OrderPayload(
            new[] { new Identifier("ip", ip) },
            null,
            null,
            IpCertProfile
        ), ct);
        var orderLoc = _lastLocation!;
        var orderObj = JsonDocument.Parse(order);
        var authzUrls = orderObj.RootElement.GetProperty("authorizations").EnumerateArray().Select(e => e.GetString()!).ToArray();
        var finalizeUrl = orderObj.RootElement.GetProperty("finalize").GetString()!;

        // For each authorization, complete http-01
        foreach (var authzUrl in authzUrls)
        {
            var authzJson = await GetAsJwsAsync(authzUrl, acctKey, await GetKidAsync(ct), ct);
            var authz = JsonSerializer.Deserialize<Authorization>(authzJson)!;
            var http01 = authz.Challenges.First(c => c.Type == "http-01");

            // Build JWK thumbprint per RFC 7638 (members ordered lexicographically)
            var jwk = GetJwk(acctKey);
            var jwkJson = JsonSerializer.Serialize(new SortedDictionary<string, string>
            {
                ["crv"] = jwk.Crv,
                ["kty"] = jwk.Kty,
                ["x"] = jwk.X,
                ["y"] = jwk.Y
            });
            var thumb = Base64Url(Sha256(Encoding.UTF8.GetBytes(jwkJson)));
            var keyAuth = http01.Token + "." + thumb;
            challenges.Set(http01.Token, keyAuth);

            // Tell the server to start validating
            _ = await PostAsJwsAsync(http01.Url, acctKey, await GetKidAsync(ct), new { }, ct);

            for (var i = 0; i < 60; i++)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
                var stJson = await GetAsJwsAsync(authzUrl, acctKey, await GetKidAsync(ct), ct);
                var st = JsonSerializer.Deserialize<Authorization>(stJson)!;
                if (st.Status == "valid") break;
                if (st.Status == "invalid")
                {
                    logger.LogError("ACME authorization invalid for {AuthzUrl}: {Body}", authzUrl, stJson);
                    throw new InvalidOperationException("ACME authorization failed");
                }
                if (i == 59) throw new TimeoutException("ACME authorization polling timed out");
            }

            challenges.Remove(http01.Token);
        }

        // Generate key and CSR.
        // LetsEncrypt rejects CSRs that contain an IP address in the Subject Common Name (CN).
        // Keep the IP only in subjectAltName:IP and use a neutral CN.
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var req = new CertificateRequest($"CN={CsrCommonName}", ecdsa, HashAlgorithmName.SHA256);
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddIpAddress(System.Net.IPAddress.Parse(ip));
        req.CertificateExtensions.Add(sanBuilder.Build());
        var csrDer = req.CreateSigningRequest();
        var csrB64 = Base64Url(csrDer);

        var fin = await PostAsJwsAsync(finalizeUrl, acctKey, await GetKidAsync(ct), new FinalizePayload(csrB64), ct);
        var ord2 = JsonDocument.Parse(fin);
        var certUrl = ord2.RootElement.TryGetProperty("certificate", out var ce) ? ce.GetString() : null;
        if (certUrl is null)
        {
            // poll order url until certificate is issued
            for (var i = 0; i < 30 && certUrl is null; i++)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
                var ordSt = await GetAsJwsAsync(orderLoc, acctKey, await GetKidAsync(ct), ct);
                var ordDoc = JsonDocument.Parse(ordSt);
                certUrl = ordDoc.RootElement.TryGetProperty("certificate", out var ce2) ? ce2.GetString() : null;
            }
        }
        if (certUrl is null) throw new InvalidOperationException("ACME finalize did not return certificate URL");

        var certPem = await GetRawAsync(certUrl, ct); // chain in PEM
        // Let's Encrypt returns a PEM chain; first cert is leaf.
        var leafPem = ExtractFirstCertificatePem(certPem);
        var cert = X509Certificate2.CreateFromPem(leafPem);
        cert = cert.CopyWithPrivateKey(ecdsa);
        return new X509Certificate2(cert.Export(X509ContentType.Pfx));
    }

    // --- ACME primitives ---
    private string? _kid;
    private string? _lastLocation;

    private async Task<DirectoryIndex> GetDirectoryAsync(CancellationToken ct)
    {
        if (_directory is not null) return _directory;
        var res = await http.GetAsync(DirectoryUrl, ct);
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadAsStringAsync(ct);
        _directory = JsonSerializer.Deserialize<DirectoryIndex>(json)!;
        return _directory;
    }

    private async Task<string> GetNonceAsync(CancellationToken ct)
    {
        var dir = await GetDirectoryAsync(ct);
        var req = new HttpRequestMessage(HttpMethod.Head, dir.NewNonce);
        var res = await http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        return res.Headers.GetValues("Replay-Nonce").First();
    }

    private static string? TryGetReplayNonce(HttpResponseMessage res)
        => res.Headers.TryGetValues("Replay-Nonce", out var vals) ? vals.FirstOrDefault() : null;

    private Task<string> GetKidAsync(CancellationToken ct)
    {
        if (_kid is null) throw new InvalidOperationException("ACME KID not set; account not created");
        return Task.FromResult(_kid);
    }

    private async Task<ECDsa> GetOrCreateAccountKeyAsync(DirectoryIndex dir, CancellationToken ct)
    {
        var store = await accountStore.LoadAsync(ct);
        if (store is { } loaded)
        {
            _kid = loaded.kid;
            return loaded.key;
        }
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var payload = new { termsOfServiceAgreed = true, contact = Array.Empty<string>() };
        var jws = await BuildJwsAsync(dir.NewAccount, key, kid: null, payload, dir, ct);
        var msg = new HttpRequestMessage(HttpMethod.Post, dir.NewAccount) { Content = JoseJson(jws) };
        var res = await http.SendAsync(msg, ct);
        try
        {
            res.EnsureSuccessStatusCode();
        }
        catch (Exception)
        {
            var errorBody = await res.Content.ReadAsStringAsync(ct);
            logger.LogError("ACME newAccount failed: {Status} {Body}", (int)res.StatusCode, errorBody);
            throw;
        }

        _kid = res.Headers.Location?.ToString();
        if (_kid is null && res.Headers.TryGetValues("Location", out var vals))
        {
            _kid = vals.FirstOrDefault();
        }
        if (_kid is null) throw new InvalidOperationException("ACME account creation did not return Location header");
        await accountStore.SaveAsync(key, _kid, ct);
        return key;
    }

    private async Task<string> PostAsJwsAsync(string url, ECDsa key, string kid, object payload, CancellationToken ct)
    {
        var dir = await GetDirectoryAsync(ct);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var jws = await BuildJwsAsync(url, key, kid, payload, dir, ct);
            var res = await http.PostAsync(url, JoseJson(jws), ct);
            _nonce = TryGetReplayNonce(res) ?? _nonce;

            if (res.IsSuccessStatusCode)
            {
                _lastLocation = res.Headers.Location?.ToString();
                if (_lastLocation is null && res.Headers.TryGetValues("Location", out var vals))
                    _lastLocation = vals.FirstOrDefault();
                return await res.Content.ReadAsStringAsync(ct);
            }

            var body = await res.Content.ReadAsStringAsync(ct);
            logger.LogError("ACME POST failed: {Url} {Status} {Body}", url, (int)res.StatusCode, body);

            if (attempt == 0 && body.Contains("badNonce", StringComparison.OrdinalIgnoreCase))
            {
                // Clear and retry once with a fresh nonce.
                _nonce = null;
                continue;
            }

            res.EnsureSuccessStatusCode();
        }

        throw new InvalidOperationException("Unreachable");
    }

    private async Task<string> GetAsJwsAsync(string url, ECDsa key, string kid, CancellationToken ct)
    {
        var dir = await GetDirectoryAsync(ct);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var jws = await BuildJwsAsync(url, key, kid, string.Empty, dir, ct);
            var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JoseJson(jws) };
            var res = await http.SendAsync(req, ct);
            _nonce = TryGetReplayNonce(res) ?? _nonce;

            if (res.IsSuccessStatusCode)
            {
                return await res.Content.ReadAsStringAsync(ct);
            }

            var body = await res.Content.ReadAsStringAsync(ct);
            logger.LogError("ACME POST-as-GET failed: {Url} {Status} {Body}", url, (int)res.StatusCode, body);

            if (attempt == 0 && body.Contains("badNonce", StringComparison.OrdinalIgnoreCase))
            {
                _nonce = null;
                continue;
            }

            res.EnsureSuccessStatusCode();
        }

        throw new InvalidOperationException("Unreachable");
    }

    // Update BuildJwsAsync to use fresh nonce and cached directory
    private async Task<string> BuildJwsAsync(string url, ECDsa key, string? kid, object payload, DirectoryIndex dir, CancellationToken ct)
    {
        var jwk = GetJwk(key);
        var protObj = new Dictionary<string, object?>
        {
            ["alg"] = "ES256",
            ["url"] = url,
            ["nonce"] = await GetNonceAsync(ct),
            [kid is null ? "jwk" : "kid"] = kid is null ? jwk : kid
        };
        var protectedB64 = Base64Url(JsonSerializer.SerializeToUtf8Bytes(protObj));
        var payloadB64 = payload is string s && s == string.Empty ? string.Empty : Base64Url(JsonSerializer.SerializeToUtf8Bytes(payload));
        var signingInput = Encoding.UTF8.GetBytes(protectedB64 + "." + payloadB64);
        var sig = key.SignData(signingInput, HashAlgorithmName.SHA256);
        var env = new JwsEnvelope(protectedB64, payloadB64, Base64Url(sig));
        return JsonSerializer.Serialize(env);
    }

    private static Jwk GetJwk(ECDsa key)
    {
        var p = key.ExportParameters(false);
        return new Jwk("EC", "P-256", Base64Url(p.Q.X!), Base64Url(p.Q.Y!));
    }

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Sha256(byte[] data)
    {
        using var sha = SHA256.Create();
        return sha.ComputeHash(data);
    }

    private static string ExtractFirstCertificatePem(string pem)
    {
        const string begin = "-----BEGIN CERTIFICATE-----";
        const string end = "-----END CERTIFICATE-----";
        var start = pem.IndexOf(begin, StringComparison.Ordinal);
        if (start < 0) throw new InvalidOperationException("No certificate in PEM");
        var finish = pem.IndexOf(end, start, StringComparison.Ordinal);
        if (finish < 0) throw new InvalidOperationException("Malformed PEM");
        finish += end.Length;
        return pem.Substring(start, finish - start);
    }

    private static StringContent JoseJson(string body)
    {
        // Some servers are strict: ACME requires Content-Type: application/jose+json
        var content = new StringContent(body, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/jose+json");
        return content;
    }

    private async Task<string> GetRawAsync(string url, CancellationToken ct)
    {
        var res = await http.GetAsync(url, ct);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadAsStringAsync(ct);
    }
}
