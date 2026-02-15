using Microsoft.Web.WebView2.Core;

namespace Concord.WinForms
{
    public partial class SettingsForm : Form
    {
        bool Initialized;
        CoreWebView2Environment WebView2Environment;

        public SettingsForm()
        {

        }

        public SettingsForm(CoreWebView2Environment webView2Environment)
        {
            WebView2Environment = webView2Environment;
            InitializeComponent();
            LoadingPanel.BringToFront();
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

        private async void HandleWebViewMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {

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
