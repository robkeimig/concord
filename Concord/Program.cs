using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Concord.Data;
using Concord.Services;
using Concord.Services.Acme;
using Concord.Endpoints;

namespace Concord;

public static class Program
{
    private record CreateAccountRequest(string? InviteCode, Profile Profile);
    private record Profile(string Name, string? PrimaryColor);
    private record CreateAccountResponse(string AccessToken);

    public static async Task Main(string[] args)
    {
        Console.WriteLine("Starting Concord...");

        var publicIp = await PublicIpService.GetPublicIpAsync();

        Console.WriteLine($"Public IP Address: {publicIp}");

        var database = new Database();
        using var sql = database.Connection;
        await sql.BootstrapUsers(publicIp);

        // ACME services
        var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
        var acmeDir = Path.Combine(dataDir, "acme");
        Directory.CreateDirectory(acmeDir);

        var builder = WebApplication.CreateBuilder(args);
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(database);
        builder.Services.AddSingleton<IAcmeHttpChallengeStore, AcmeHttpChallengeStore>();
        builder.Services.AddSingleton<IAcmeAccountStore>(_ => new FileAcmeAccountStore(acmeDir));
        builder.Services.AddHttpClient<IAcmeClient, AcmeClient>().ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(30));
        builder.Services.AddRazorPages();

        object certLock = new();
        X509Certificate2? currentCert = null;
        DateTimeOffset? issuedAtUtc = null;
        string? issuedForIp = null;

        IServiceProvider? serviceProvider = null;

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenAnyIP(80);

            options.ListenAnyIP(443, listenOptions =>
            {
                listenOptions.UseHttps(httpsOptions =>
                {
                    httpsOptions.ServerCertificateSelector = (connectionContext, name) =>
                    {
                        lock (certLock)
                        {
                            var now = DateTimeOffset.UtcNow;
                            var needsRenew = currentCert is null
                                || issuedAtUtc is null
                                || (now - issuedAtUtc.Value) >= TimeSpan.FromHours(24);

                            if (!needsRenew)
                                return currentCert!;

                            var sp = serviceProvider ?? throw new InvalidOperationException("ServiceProvider not initialized");
                            using var scope = sp.CreateScope();
                            var acme = scope.ServiceProvider.GetRequiredService<IAcmeClient>();

                            Console.WriteLine($"Issuing/renewing Let's Encrypt certificate for {publicIp}");

                            try
                            {
                                currentCert = acme.EnsureIpCertificateAsync(publicIp, CancellationToken.None).GetAwaiter().GetResult();
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Failed to issue Let's Encrypt certificate for {publicIp}: {ex.Message}");
                                throw;
                            }

                            issuedAtUtc = now;
                            issuedForIp = publicIp;

                            Console.WriteLine($"Let's Encrypt certificate for {publicIp} [re]issued successfully.");
                            return currentCert;
                        }
                    };
                });
            });
        });

        var app = builder.Build();
        serviceProvider = app.Services;

        // Server capability discovery for clients
        app.MapGet("/ServerInfo", (Database db) =>
        {
            using var sql = db.Connection;
            var hasInvite = !string.IsNullOrWhiteSpace(sql.GetLatestInvitationCode());

            return Results.Json(new
            {
                requiresInvitation = hasInvite,
            });
        });

        app.MapPost("/CreateAccount", (CreateAccountRequest req, Database db) =>
        {
            if (req.Profile is null || string.IsNullOrWhiteSpace(req.Profile.Name))
                return Results.BadRequest(new { message = "Profile name is required." });

            using var sql = db.Connection;
            var latestInvite = sql.GetLatestInvitationCode();
            var requiresInvite = !string.IsNullOrWhiteSpace(latestInvite);

            if (requiresInvite)
            {
                if (string.IsNullOrWhiteSpace(req.InviteCode) || !string.Equals(req.InviteCode.Trim(), latestInvite, StringComparison.Ordinal))
                    return Results.Unauthorized();
            }

            // TODO: persist user/profile. For now issue a bearer token that can be stored per-server by the client.
            var tokenBytes = RandomNumberGenerator.GetBytes(32);
            var token = Convert.ToBase64String(tokenBytes);

            return Results.Json(new CreateAccountResponse(token));
        });

        app.Lifetime.ApplicationStarted.Register(async () =>
        {
            Console.WriteLine("Concord started successfully.");
        });

        app.UseWebSockets(new WebSocketOptions
        {
            KeepAliveInterval = TimeSpan.FromSeconds(30)
        });

        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.MapAcmeEndpoints();
        app.MapConcordEndpoints();
        app.MapRazorPages();
        app.Run();
    }
}