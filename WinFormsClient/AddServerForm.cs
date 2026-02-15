using Microsoft.Web.WebView2.Core;
using System.Text.Json;
using WinFormsClient;

namespace Concord.WinForms
{
    public partial class AddServerForm : Form
    {
        private bool Initialized;
        private readonly CoreWebView2Environment WebView2Environment;
        private readonly Configuration Configuration;
        private readonly Rectangle? TargetBounds;

        // Pending add flow state (kept in memory until the user finishes the web-based wizard)
        private AddServerRequest? PendingAddServerRequest;

        private record AddServerRequest(string IpAddress, string InvitationToken);

        public AddServerForm(CoreWebView2Environment webView2Environment, Configuration configuration, Rectangle? targetBounds = null)
        {
            WebView2Environment = webView2Environment ?? throw new ArgumentNullException(nameof(webView2Environment));
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            TargetBounds = targetBounds;

            InitializeComponent();

            // Borderless window (no title bar) without Win32 interop.
            FormBorderStyle = FormBorderStyle.None;
            ControlBox = false;
            Text = string.Empty;

            // We'll explicitly place it to match the main WebView bounds when shown.
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;

            Shown += (_, _) => ApplyTargetBoundsIfProvided();

            LoadingPanel.BringToFront();
            LoadingPanel.Visible = true;

            EnsureWebView();
        }

        private void CancelAndClose()
        {
            PendingAddServerRequest = null;
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void ApplyTargetBoundsIfProvided()
        {
            if (TargetBounds is null)
                return;

            SuspendLayout();
            try
            {
                Bounds = TargetBounds.Value;
            }
            finally
            {
                ResumeLayout(performLayout: true);
            }
        }

        private async void EnsureWebView()
        {
            await AddServerWebView.EnsureCoreWebView2Async(WebView2Environment);
            AddServerWebView.NavigationCompleted += HandleWebViewNavigationCompleted;
            AddServerWebView.CoreWebView2.WebMessageReceived += HandleWebViewMessageReceived;
            AddServerWebView.DefaultBackgroundColor = Color.Transparent;

            AddServerWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                hostName: "app",
                folderPath: Path.Combine(AppContext.BaseDirectory, "wwwroot"),
                accessKind: CoreWebView2HostResourceAccessKind.Allow
            );

            PendingAddServerRequest = null;
            AddServerWebView.Source = new Uri("https://app/AddServer.html");
        }

        private void PostJsonToWebView(object message)
        {
            if (AddServerWebView?.CoreWebView2 is null)
                return;

            var json = JsonSerializer.Serialize(
                message,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));

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

                if (string.Equals(type, "addServer", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = root.TryGetProperty("payload", out var p) ? p : default;
                    var ipAddress = GetPropertyOrDefault(payload, "ipAddress", p2 => p2.GetString() ?? string.Empty)?.Trim();
                    var invitationToken = GetPropertyOrDefault(payload, "invitationToken", p2 => p2.GetString() ?? string.Empty)?.Trim();

                    if (string.IsNullOrWhiteSpace(ipAddress) || string.IsNullOrWhiteSpace(invitationToken))
                        return;

                    var request = new AddServerRequest(ipAddress, invitationToken);

                    var isValid = await ValidateInvitationAsync(request.IpAddress, request.InvitationToken);
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
                    PostJsonToWebView(new { type = "addServer/validated", payload = new { ok = true } });
                    return;
                }

                // Current AddServer.html sends this message on step 2.
                if (string.Equals(type, "addServer/createMember", StringComparison.OrdinalIgnoreCase))
                {
                    if (PendingAddServerRequest is null)
                    {
                        PostJsonToWebView(new
                        {
                            type = "addServer/memberCreated",
                            payload = new { ok = false, message = "No pending invitation to accept." }
                        });
                        return;
                    }

                    var payload = root.TryGetProperty("payload", out var p) ? p : default;
                    var name = GetPropertyOrDefault(payload, "name", p2 => p2.GetString() ?? string.Empty)?.Trim();

                    if (string.IsNullOrWhiteSpace(name))
                    {
                        PostJsonToWebView(new
                        {
                            type = "addServer/memberCreated",
                            payload = new { ok = false, message = "Please provide a name." }
                        });
                        return;
                    }

                    var request = PendingAddServerRequest;

                    var accepted = await AcceptInvitationAsync(request.IpAddress, request.InvitationToken, name);
                    if (!accepted)
                    {
                        PostJsonToWebView(new
                        {
                            type = "addServer/memberCreated",
                            payload = new { ok = false, message = "Failed to accept invitation." }
                        });
                        return;
                    }

                    var server = new Server
                    {
                        Name = name,
                        IpAddress = request.IpAddress,
                    };

                    Configuration.Servers.Add(server);
                    Configuration.LastServerId = server.Id;
                    Configuration.SaveChanges();

                    PendingAddServerRequest = null;

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

        // Stubs: later these should call https://<ip>/ValidateInvitation and https://<ip>/AcceptInvitation
        private static Task<bool> ValidateInvitationAsync(string ipAddress, string invitationToken)
            => Task.FromResult(true);

        private static Task<bool> AcceptInvitationAsync(string ipAddress, string invitationToken, string name)
            => Task.FromResult(true);

        private void HandleWebViewNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            LoadingPanel.Visible = false;

            if (!Initialized)
            {
                Initialized = true;
            }
        }
    }
}
