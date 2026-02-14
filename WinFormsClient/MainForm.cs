using Microsoft.Web.WebView2.Core;

namespace WinFormsClient
{
    public partial class MainForm : Form
    {
        bool Initialized;
        Configuration Configuration;

        // Pending add flow state (kept in memory until the user finishes the web-based wizard)
        private AddServerRequest? PendingAddServerRequest;

        public MainForm()
        {
            InitializeComponent();
            LoadingPanel.BringToFront();
            Configuration = Configuration.Load();

            AddNewServerButton.Click += (_, _) => NavigateToAddServer();

            EnsureWebView();
            //ApplyDarkTheme(this);
        }

        private void NavigateToAddServer()
        {
            if (MainWebView?.CoreWebView2 == null)
            {
                // WebView is still initializing; fall back to setting Source.
                MainWebView.Source = new Uri("https://app/AddServer.html");
                return;
            }

            PendingAddServerRequest = null;
            MainWebView.CoreWebView2.Navigate("https://app/AddServer.html");
        }

        private async void EnsureWebView()
        {
            var environment = await CoreWebView2Environment.CreateAsync(
               userDataFolder: Path.Combine(
                   Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                   "ConcordWebView"
               )
           );

            await MainWebView.EnsureCoreWebView2Async(environment);
            MainWebView.NavigationCompleted += HandleWebViewNavigationCompleted;
            MainWebView.CoreWebView2.WebMessageReceived += HandleWebViewMessageReceived;

            MainWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                hostName: "app",
                folderPath: Path.Combine(AppContext.BaseDirectory, "wwwroot"),
                accessKind: CoreWebView2HostResourceAccessKind.Allow
            );

            if (Configuration.Servers.Count == 0)
            {
                MainWebView.Source = new Uri("https://app/NoServers.html");
            }
            else
            {
                var server = Configuration.LastServerId.HasValue
                    ? Configuration.Servers.First(x => x.Id == Configuration.LastServerId.Value)
                    : Configuration.Servers.First();

                MainWebView.Source = new Uri($"https://{server.IpAddress}");
            }
        }

        private record AddServerRequest(string ipAddress, string invitationToken);

        private async void HandleWebViewMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            var json = e.WebMessageAsJson;
            if (string.IsNullOrWhiteSpace(json))
                return;

            string? type;
            string? ipAddress;
            string? invitationToken;
            string? name;

            // IMPORTANT: don't store JsonElement outside the JsonDocument lifetime
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;

                type = root.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;

                ipAddress = null;
                invitationToken = null;
                name = null;

                if (root.TryGetProperty("payload", out var payload) && payload.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    if (payload.TryGetProperty("ipAddress", out var ipEl))
                        ipAddress = ipEl.GetString();

                    if (payload.TryGetProperty("invitationToken", out var tokEl))
                        invitationToken = tokEl.GetString();

                    if (payload.TryGetProperty("name", out var nameEl))
                        name = nameEl.GetString();
                }
            }
            catch
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(type))
                return;

            if (string.Equals(type, "addServer", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(ipAddress) || string.IsNullOrWhiteSpace(invitationToken))
                    return;

                var request = new AddServerRequest(ipAddress.Trim(), invitationToken.Trim());

                // Stubbed for now.
                var isValid = await ValidateInvitationAsync(request.ipAddress, request.invitationToken);
                if (!isValid)
                {
                    PostJsonToWebView(new
                    {
                        type = "addServer/validated",
                        payload = new { ok = false, message = "Invitation is not valid." }
                    });
                    return;
                }

                PendingAddServerRequest = request;

                PostJsonToWebView(new
                {
                    type = "addServer/validated",
                    payload = new { ok = true }
                });

                return;
            }

            if (string.Equals(type, "addServer/accept", StringComparison.OrdinalIgnoreCase))
            {
                if (PendingAddServerRequest is null)
                {
                    PostJsonToWebView(new
                    {
                        type = "addServer/accepted",
                        payload = new { ok = false, message = "No pending invitation to accept." }
                    });
                    return;
                }

                if (string.IsNullOrWhiteSpace(name))
                {
                    PostJsonToWebView(new
                    {
                        type = "addServer/accepted",
                        payload = new { ok = false, message = "Please provide a name." }
                    });
                    return;
                }

                var request = PendingAddServerRequest;

                var accepted = await AcceptInvitationAsync(request.ipAddress, request.invitationToken, name);
                if (!accepted)
                {
                    PostJsonToWebView(new
                    {
                        type = "addServer/accepted",
                        payload = new { ok = false, message = "Failed to accept invitation." }
                    });
                    return;
                }

                var server = new Server
                {
                    Name = name.Trim(),
                    IpAddress = request.ipAddress.Trim(),
                };

                Configuration.Servers.Add(server);
                Configuration.LastServerId = server.Id;
                Configuration.SaveChanges();

                PendingAddServerRequest = null;

                PostJsonToWebView(new
                {
                    type = "addServer/accepted",
                    payload = new { ok = true }
                });

                MainWebView.Source = new Uri($"https://{server.IpAddress}");
            }
        }

        private void PostJsonToWebView(object message)
        {
            if (MainWebView?.CoreWebView2 is null)
                return;

            var json = System.Text.Json.JsonSerializer.Serialize(
                message,
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

            MainWebView.CoreWebView2.PostWebMessageAsJson(json);
        }

        // Stubs: later these should call https://<ip>/ValidateInvitation and https://<ip>/AcceptInvitation
        private static Task<bool> ValidateInvitationAsync(string ipAddress, string invitationToken)
            => Task.FromResult(true);

        private static Task<bool> AcceptInvitationAsync(string ipAddress, string invitationToken, string name)
            => Task.FromResult(true);

        private void HandleWebViewNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!Initialized)
            {
                LoadingPanel.Visible = false;
                Initialized = true;
            }
        }
    }
}
