using Microsoft.Web.WebView2.Core;
using WinFormsClient;

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

            Text = "Settings";
            StartPosition = FormStartPosition.CenterParent;
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

            var json = System.Text.Json.JsonSerializer.Serialize(
                message,
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

            SettingsWebView.CoreWebView2.PostWebMessageAsJson(json);
        }

        private void HandleWebViewMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            var json = e.WebMessageAsJson;
            if (string.IsNullOrWhiteSpace(json))
                return;

            string? type;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;
                type = root.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;
            }
            catch
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(type))
                return;

            if (string.Equals(type, "settings/close", StringComparison.OrdinalIgnoreCase))
            {
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            // Placeholder for future settings actions; acknowledge receipt so the page can evolve.
            PostJsonToWebView(new { type = "settings/ack", payload = new { received = type } });
        }

        private void HandleWebViewNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            // Hide loading UI once navigation completes.
            LoadingPanel.Visible = false;

            if (!Initialized)
            {
                Initialized = true;
            }
        }
    }
}
