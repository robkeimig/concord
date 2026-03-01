using Microsoft.Web.WebView2.Core;
using System.Net.Http.Json;
using System.Text.Json;
using WinFormsClient;

namespace Concord.WinForms
{
    public partial class AddServerForm : Form
    {
        private bool Initialized;
        private readonly CoreWebView2Environment WebViewEnvironment;
        private readonly Configuration Configuration;

        private string? PendingIpAddress;
        private bool? PendingRequiresInvitation;

        private record ServerInfo(bool RequiresInvitation);

        private record CreateAccountRequest(
            string? InviteCode,
            ProfileDetails Profile
        );

        private record ProfileDetails(
            string Name,
            string? PrimaryColor
        );

        private record CreateAccountResponse(string AccessToken);

        public AddServerForm(CoreWebView2Environment webView2Environment, Configuration configuration)
        {
            WebViewEnvironment = webView2Environment ?? throw new ArgumentNullException(nameof(webView2Environment));
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            InitializeComponent();

            // Borderless window (no title bar) without Win32 interop.
            FormBorderStyle = FormBorderStyle.None;
            ControlBox = false;
            Text = string.Empty;

            // We'll explicitly place it to match the main WebView bounds when shown.
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;

            LoadingPanel.BringToFront();
            LoadingPanel.Visible = true;
        }

        private void CancelAndClose()
        {
            PendingIpAddress = null;
            PendingRequiresInvitation = null;
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void PostJsonToWebView(object message)
        {
            if (AddServerWebView?.CoreWebView2 is null)
                return;

            var json = JsonSerializer.Serialize(message, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            AddServerWebView.CoreWebView2.PostWebMessageAsJson(json);
        }

        private static T? GetPropertyOrDefault<T>(JsonElement root, string name, Func<JsonElement, T> convert)
        {
            return root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var el) && el.ValueKind != JsonValueKind.Null
                ? convert(el)
                : default;
        }

        private async void HandleWebViewMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            var json = e.WebMessageAsJson;
            if (string.IsNullOrWhiteSpace(json))
                return;

            JsonDocument? doc = null;
            try
            {
                doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var type = GetPropertyOrDefault(root, "type", p => p.GetString());
                if (string.IsNullOrWhiteSpace(type))
                    return;

                if (string.Equals(type, "addServer/ip", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = root.TryGetProperty("payload", out var p) ? p : default;
                    var ipAddress = GetPropertyOrDefault(payload, "ipAddress", p2 => p2.GetString() ?? string.Empty)?.Trim();

                    if (string.IsNullOrWhiteSpace(ipAddress))
                        return;

                    PendingIpAddress = ipAddress;

                    var serverInfo = await GetServerInfoAsync(ipAddress);
                    if (serverInfo is null)
                    {
                        PostJsonToWebView(new
                        {
                            type = "addServer/serverInfo",
                            payload = new { ok = false, message = "Unable to reach server." }
                        });
                        return;
                    }

                    PendingRequiresInvitation = serverInfo.RequiresInvitation;

                    PostJsonToWebView(new
                    {
                        type = "addServer/serverInfo",
                        payload = new { ok = true, requiresInvitation = serverInfo.RequiresInvitation }
                    });

                    return;
                }

                if (string.Equals(type, "addServer/createMember", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = root.TryGetProperty("payload", out var p) ? p : default;

                    var ipAddress = GetPropertyOrDefault(payload, "ipAddress", p2 => p2.GetString() ?? string.Empty)?.Trim();
                    var invitationToken = GetPropertyOrDefault(payload, "invitationToken", p2 => p2.GetString() ?? string.Empty)?.Trim();
                    var name = GetPropertyOrDefault(payload, "name", p2 => p2.GetString() ?? string.Empty)?.Trim();
                    var primaryColor = GetPropertyOrDefault(payload, "primaryColor", p2 => p2.GetString() ?? string.Empty)?.Trim();

                    if (string.IsNullOrWhiteSpace(ipAddress))
                        ipAddress = PendingIpAddress;

                    if (string.IsNullOrWhiteSpace(ipAddress))
                    {
                        PostJsonToWebView(new
                        {
                            type = "addServer/memberCreated",
                            payload = new { ok = false, message = "No server IP address provided." }
                        });
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(name))
                    {
                        PostJsonToWebView(new
                        {
                            type = "addServer/memberCreated",
                            payload = new { ok = false, message = "Please provide a name." }
                        });
                        return;
                    }

                    // If we haven't discovered server info yet, do it now.
                    var requiresInvitation = PendingRequiresInvitation;
                    if (requiresInvitation is null)
                    {
                        var si = await GetServerInfoAsync(ipAddress);
                        requiresInvitation = si?.RequiresInvitation;
                        PendingRequiresInvitation = requiresInvitation;
                    }

                    if (requiresInvitation == true)
                    {
                        if (string.IsNullOrWhiteSpace(invitationToken))
                        {
                            PostJsonToWebView(new
                            {
                                type = "addServer/memberCreated",
                                payload = new { ok = false, message = "Invitation token is required." }
                            });
                            return;
                        }

                        var isValid = await ValidateInvitationAsync(ipAddress, invitationToken, name);
                        if (!isValid)
                        {
                            PostJsonToWebView(new
                            {
                                type = "addServer/memberCreated",
                                payload = new { ok = false, message = "Invitation is not valid." }
                            });
                            return;
                        }
                    }

                    var accessToken = await CreateAccountAsync(ipAddress, invitationToken ?? string.Empty, name, primaryColor);
                    if (string.IsNullOrWhiteSpace(accessToken))
                    {
                        PostJsonToWebView(new
                        {
                            type = "addServer/memberCreated",
                            payload = new { ok = false, message = "Failed to create account." }
                        });
                        return;
                    }

                    var server = new Server
                    {
                        Name = name,
                        IpAddress = ipAddress,
                        AccessToken = accessToken,
                    };

                    Configuration.Servers.Add(server);
                    Configuration.LastServerId = server.Id;
                    Configuration.SaveChanges();

                    PendingIpAddress = null;
                    PendingRequiresInvitation = null;

                    PostJsonToWebView(new { type = "addServer/memberCreated", payload = new { ok = true } });

                    DialogResult = DialogResult.OK;
                    Close();
                    return;
                }

                if (string.Equals(type, "addServer/cancel", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(type, "addServer/close", StringComparison.OrdinalIgnoreCase))
                {
                    CancelAndClose();
                    return;
                }

                PostJsonToWebView(new { type = "addServer/ack", payload = new { received = type } });
            }
            catch
            {
                return;
            }
            finally
            {
                doc?.Dispose();
            }
        }

        private static async Task<ServerInfo?> GetServerInfoAsync(string ipAddress)
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                var uri = ServerUri.GetUri(ipAddress, "ServerInfo");
                var serverInfo = await http.GetFromJsonAsync<ServerInfo>(uri);
                if (serverInfo is not null)
                    return serverInfo;
                return null;
            }
            catch
            {
                return null;
            }
        }

        private static async Task<string?> CreateAccountAsync(string ipAddress, string inviteCode, string name, string? primaryColor)
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

                var request = new CreateAccountRequest(
                    InviteCode: string.IsNullOrWhiteSpace(inviteCode) ? null : inviteCode,
                    Profile: new ProfileDetails(
                        Name: name,
                        PrimaryColor: string.IsNullOrWhiteSpace(primaryColor) ? null : primaryColor
                    )
                );

                var res = await http.PostAsJsonAsync(ServerUri.GetUri(ipAddress, "CreateAccount"), request, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                if (!res.IsSuccessStatusCode)
                    return null;

                var body = await res.Content.ReadFromJsonAsync<CreateAccountResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
                return body?.AccessToken;
            }
            catch
            {
                return null;
            }
        }

        // Stubs: host should call server endpoints. For now keep in client.
        private static Task<bool> ValidateInvitationAsync(string ipAddress, string invitationToken, string name)
            => Task.FromResult(true);

        private void HandleWebViewNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            LoadingPanel.Visible = false;

            if (!Initialized)
            {
                Initialized = true;
            }
        }

        internal async Task InitializeAsync()
        {
            await AddServerWebView.EnsureCoreWebView2Async(WebViewEnvironment);
            AddServerWebView.NavigationCompleted += HandleWebViewNavigationCompleted;
            AddServerWebView.CoreWebView2.WebMessageReceived += HandleWebViewMessageReceived;
            AddServerWebView.DefaultBackgroundColor = Color.Transparent;

            AddServerWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                hostName: "app",
                folderPath: Path.Combine(AppContext.BaseDirectory, "wwwroot"),
                accessKind: CoreWebView2HostResourceAccessKind.Allow
            );

            PendingIpAddress = null;
            PendingRequiresInvitation = null;
            AddServerWebView.Source = new Uri("https://app/AddServer.html");
        }
    }
}
