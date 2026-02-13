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
            Configuration = Configuration.Load();
            EnsureWebView();
        }

        private async void EnsureWebView()
        {
            //LoadingPanel.BringToFront();

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

            if (Configuration.Servers.Count == 0)
            {
                //Provide instructions on adding or creating first server.
                MainWebView.Source = new Uri($"https://app/NoServers.html");
            }
            else
            {
                //Get the last server we connected to if available.
                //Otherwise, grab the first one in natural list order.
                var server = Configuration.LastServerId.HasValue
                    ? Configuration.Servers.First(x => x.Id == Configuration.LastServerId.Value)
                    : Configuration.Servers.First();

                MainWebView.Source = new Uri($"https://{server.IpAddress}");
            }
        }

        private void HandleWebViewNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!Initialized)
            {
                //LoadingPanel.Visible = false;
                Initialized = true;
            }
        }
    }
}
