using Microsoft.Web.WebView2.Core;

namespace WinFormsClient
{
    public partial class MainForm : Form
    {
        bool Initialized;
        Configuration Configuration;

        public MainForm()
        {
            InitializeComponent();
            LoadingPanel.BringToFront();
            Configuration = Configuration.Load();

            AddNewServerButton.Click += (_, _) => ShowAddServerDialog();

            EnsureWebView();
            //ApplyDarkTheme(this);
        }

        private void ShowAddServerDialog()
        {
            if (MainWebView?.CoreWebView2 is null)
                return;

            var env = MainWebView.CoreWebView2.Environment;
            var targetBounds = MainWebView.RectangleToScreen(MainWebView.ClientRectangle);

            using var dlg = new Concord.WinForms.AddServerForm(env, Configuration, targetBounds)
            {
                ShowInTaskbar = false,
                MinimizeBox = false,
                MaximizeBox = true
            };

            var result = dlg.ShowDialog(this);
            if (result == DialogResult.OK)
            {
                // Reload configuration + navigate to the newly-selected server.
                Configuration = Configuration.Load();
                NavigateToCurrentServer();
            }
        }

        private void NavigateToCurrentServer()
        {
            if (Configuration.Servers.Count == 0)
            {
                MainWebView.Source = new Uri("https://app/NoServers.html");
                return;
            }

            var server = Configuration.LastServerId.HasValue
                ? Configuration.Servers.First(x => x.Id == Configuration.LastServerId.Value)
                : Configuration.Servers.First();

            MainWebView.Source = new Uri($"https://{server.IpAddress}");
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

            MainWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                hostName: "app",
                folderPath: Path.Combine(AppContext.BaseDirectory, "wwwroot"),
                accessKind: CoreWebView2HostResourceAccessKind.Allow
            );

            NavigateToCurrentServer();
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

        private void SettingsButton_Click(object sender, EventArgs e)
        {
            // Present the SettingsForm as a modal dialog, passing the WebView2 environment for optimal performance.
            if (MainWebView?.CoreWebView2 is null)
                return;

            var env = MainWebView.CoreWebView2.Environment;

            // Match the on-screen bounds of the embedded web view.
            var targetBounds = MainWebView.RectangleToScreen(MainWebView.ClientRectangle);

            using var dlg = new Concord.WinForms.SettingsForm(env, Configuration, targetBounds)
            {
                ShowInTaskbar = false,
                MinimizeBox = false,
                MaximizeBox = true
            };

            dlg.ShowDialog(this);
        }
    }
}
