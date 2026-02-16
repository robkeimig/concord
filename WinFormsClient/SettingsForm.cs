using Microsoft.Web.WebView2.Core;
using WinFormsClient;
using System.Text.Json;

namespace Concord.WinForms
{
    public partial class SettingsForm : Form
    {
        private bool Initialized;
        private readonly CoreWebView2Environment WebView2Environment;
        private readonly Configuration Configuration;

        public SettingsForm(CoreWebView2Environment webView2Environment, Configuration configuration)
        {
            WebView2Environment = webView2Environment ?? throw new ArgumentNullException(nameof(webView2Environment));
            Configuration = configuration;

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
            EnsureWebView();
        }

        private async void EnsureWebView()
        {
            await SettingsWebView.EnsureCoreWebView2Async(WebView2Environment);
            SettingsWebView.NavigationCompleted += HandleWebViewNavigationCompleted;
            SettingsWebView.CoreWebView2.WebMessageReceived += HandleWebViewMessageReceived;
            SettingsWebView.DefaultBackgroundColor = Color.Transparent;

            SettingsWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                hostName: "app",
                folderPath: Path.Combine(AppContext.BaseDirectory, "wwwroot"),
                accessKind: CoreWebView2HostResourceAccessKind.Allow
            );
            
            SettingsWebView.Source = new Uri("https://app/Settings.html");
        }

        private void PostJsonToWebView(object message)
        {
            if (SettingsWebView?.CoreWebView2 is null)
                return;

            var json = JsonSerializer.Serialize(
                message,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));

            SettingsWebView.CoreWebView2.PostWebMessageAsJson(json);
        }

        private object CreateSettingsPayload()
        {
            return new
            {
                audioInputDevice = Configuration.AudioInputDevice,
                audioOutputDevice = Configuration.AudioOutputDevice,
                videoInputDevice = Configuration.VideoInputDevice,
                pushToTalkKey = Configuration.PushToTalkKey.ToString()
            };
        }

        private static T? GetPropertyOrDefault<T>(JsonElement root, string name, Func<JsonElement, T> convert)
        {
            return root.TryGetProperty(name, out var el) && el.ValueKind != JsonValueKind.Null
                ? convert(el)
                : default;
        }

        private void HandleWebViewMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            var json = e.WebMessageAsJson;
            if (string.IsNullOrWhiteSpace(json))
                return;

            JsonDocument? doc = null;
            try
            {
                doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var type = root.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;
                if (string.IsNullOrWhiteSpace(type))
                    return;

                if (string.Equals(type, "settings/load", StringComparison.OrdinalIgnoreCase))
                {
                    PostJsonToWebView(new { type = "settings/data", payload = CreateSettingsPayload() });
                    return;
                }

                if (string.Equals(type, "settings/cancel", StringComparison.OrdinalIgnoreCase))
                {
                    DialogResult = DialogResult.Cancel;
                    Close();
                    return;
                }

                if (string.Equals(type, "settings/save", StringComparison.OrdinalIgnoreCase))
                {
                    if (!root.TryGetProperty("payload", out var payload))
                        payload = default;

                    // Update config from payload
                    var audioIn = GetPropertyOrDefault(payload, "audioInputDevice", p => p.GetString() ?? string.Empty);
                    var audioOut = GetPropertyOrDefault(payload, "audioOutputDevice", p => p.GetString() ?? string.Empty);
                    var videoIn = GetPropertyOrDefault(payload, "videoInputDevice", p => p.GetString() ?? string.Empty);
                    var pttKeyStr = GetPropertyOrDefault(payload, "pushToTalkKey", p => p.GetString() ?? string.Empty);

                    if (audioIn is not null)
                        Configuration.AudioInputDevice = audioIn;
                    if (audioOut is not null)
                        Configuration.AudioOutputDevice = audioOut;
                    if (videoIn is not null)
                        Configuration.VideoInputDevice = videoIn;

                    if (!string.IsNullOrWhiteSpace(pttKeyStr) && Enum.TryParse<Keys>(pttKeyStr, ignoreCase: true, out var parsedKey))
                        Configuration.PushToTalkKey = parsedKey;

                    Configuration.SaveChanges();

                    DialogResult = DialogResult.OK;
                    Close();
                    return;
                }

                if (string.Equals(type, "settings/pttKeyCaptured", StringComparison.OrdinalIgnoreCase))
                {
                    if (!root.TryGetProperty("payload", out var payload))
                        return;

                    var keyStr = GetPropertyOrDefault(payload, "key", p => p.GetString() ?? string.Empty);
                    if (!string.IsNullOrWhiteSpace(keyStr) && Enum.TryParse<Keys>(keyStr, ignoreCase: true, out var parsedKey))
                    {
                        Configuration.PushToTalkKey = parsedKey;
                        PostJsonToWebView(new { type = "settings/data", payload = CreateSettingsPayload() });
                    }

                    return;
                }

                if (string.Equals(type, "settings/close", StringComparison.OrdinalIgnoreCase))
                {
                    DialogResult = DialogResult.OK;
                    Close();
                    return;
                }

                // Placeholder for future settings actions; acknowledge receipt so the page can evolve.
                PostJsonToWebView(new { type = "settings/ack", payload = new { received = type } });
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

        private void HandleWebViewNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            // Hide loading UI once navigation completes.
            LoadingPanel.Visible = false;

            if (!Initialized)
            {
                Initialized = true;
                PostJsonToWebView(new { type = "settings/data", payload = CreateSettingsPayload() });
            }
        }
    }
}
