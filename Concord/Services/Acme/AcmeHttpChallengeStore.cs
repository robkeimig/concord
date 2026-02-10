using System.Collections.Concurrent;

namespace Concord.Services.Acme;

public interface IAcmeHttpChallengeStore
{
    void Set(string token, string keyAuthorization);
    bool TryGet(string token, out string keyAuthorization);
    void Remove(string token);
}

public sealed class AcmeHttpChallengeStore : IAcmeHttpChallengeStore
{
    private readonly ConcurrentDictionary<string, string> _challenges = new();

    public void Set(string token, string keyAuthorization)
        => _challenges[token] = keyAuthorization;

    public bool TryGet(string token, out string keyAuthorization)
        => _challenges.TryGetValue(token, out keyAuthorization!);

    public void Remove(string token)
        => _challenges.TryRemove(token, out _);
}
