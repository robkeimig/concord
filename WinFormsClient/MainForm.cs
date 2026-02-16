using Concord.WinForms;
using Microsoft.Web.WebView2.Core;

namespace WinFormsClient
{
    public partial class MainForm : Form
    {
        bool Initialized;
        Configuration Configuration;
        SettingsForm SettingsForm;
        AddServerForm AddServerForm;

        public MainForm()
        {
            InitializeComponent();
            LoadingPanel.BringToFront();
            Configuration = Configuration.Load();

            AddNewServerButton.Click += (_, _) => ShowAddServerDialog();

            EnsureWebView();
            //ApplyDarkTheme(this);
        }

        private sealed class StatusStripScope : IDisposable
        {
            private readonly StatusStrip _strip;
            private readonly bool _wasEnabled;

            public StatusStripScope(StatusStrip strip)
            {
                _strip = strip;
                _wasEnabled = strip.Enabled;

                // Disable so it can't be interacted with.
                _strip.Enabled = false;
            }

            public void Dispose()
            {
                _strip.Enabled = _wasEnabled;
            }
        }

        private StatusStripScope SuppressMainStatusStrip()
        {
            // `MainStatusStrip` is the designer-created instance.
            return new StatusStripScope(MainStatusStrip);
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

            SettingsForm = new SettingsForm(environment, Configuration);
            AddServerForm = new AddServerForm(environment, Configuration);

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

        private void ShowAddServerDialog()
        {
            using (SuppressMainStatusStrip())
            {
                AddServerForm.StartPosition = FormStartPosition.CenterParent;
                var result = AddServerForm.ShowDialog(this);

                if (result == DialogResult.OK)
                {
                    
                    Configuration = Configuration.Load();
                    NavigateToCurrentServer();
                }
            }
        }

        private void ShowSettingsDialog(object sender, EventArgs e)
        {
            using (SuppressMainStatusStrip())
            {
                SettingsForm.StartPosition = FormStartPosition.CenterParent;
                SettingsForm.ShowDialog(this);
            }
        }
    }
}
