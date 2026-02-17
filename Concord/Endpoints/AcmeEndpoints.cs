using System.Text;
using Concord.Services.Acme;

namespace Concord.Endpoints;

public static class AcmeEndpoints
{
    public static IEndpointRouteBuilder MapAcmeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/.well-known/acme-challenge/{token}", (string token, IAcmeHttpChallengeStore store) =>
        {
            if (store.TryGet(token, out var keyAuth))
                return Results.Text(keyAuth, "text/plain", Encoding.UTF8);

            return Results.NotFound();
        });

        return endpoints;
    }
}