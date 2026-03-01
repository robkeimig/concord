using Concord.WinForms;
using Microsoft.Web.WebView2.Core;
using System;
using System.Diagnostics;

namespace WinFormsClient;

public partial class MainForm : Form
{
    bool Initialized;
    Configuration Configuration;
    SettingsForm SettingsForm;
    AddServerForm AddServerForm;
    HttpClient PingHttpClient;
    CoreWebView2Environment WebViewEnvironment;
    private ToolStripSeparator? ServerListSeparator;
    TaskCompletionSource NavigationTaskCompletionSource;

    public MainForm(CoreWebView2Environment webViewEnvironment, Configuration configuration)
    {
        WebViewEnvironment = webViewEnvironment;
        PingHttpClient = new HttpClient();
        InitializeComponent();
        LoadingPanel.BringToFront();
        Configuration = configuration; 
        AddNewServerButton.Click += (_, _) => ShowAddServerDialog();
        RefreshServerListMenu();
    }

    private sealed class StatusStripScope : IDisposable
    {
        private readonly StatusStrip _strip;
        private readonly bool _wasEnabled;

        public StatusStripScope(StatusStrip strip)
        {
            _strip = strip;
            _wasEnabled = strip.Enabled;
            _strip.Enabled = false;
        }

        public void Dispose()
        {
            _strip.Enabled = _wasEnabled;
        }
    }

    private sealed class ModalOverlayScope : IDisposable
    {
        private readonly Form _owner;
        private readonly Panel _overlayPanel;
        private readonly Control _loading;

        private Form? _overlayForm;
        private readonly bool _overlayPanelWasVisible;

        public ModalOverlayScope(Form owner, Panel overlayPanel, Control loading)
        {
            _owner = owner;
            _overlayPanel = overlayPanel;
            _loading = loading;

            _overlayPanelWasVisible = overlayPanel.Visible;

            _overlayForm = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                BackColor = Color.Black,
                Opacity = 0.75,
                //TopMost = true,
                Owner = owner,
            };

            _overlayForm.Bounds = owner.Bounds;

            owner.LocationChanged += OwnerBoundsChanged;
            owner.SizeChanged += OwnerBoundsChanged;

            _overlayForm.Show(owner);
            _overlayForm.BringToFront();

            _loading.BringToFront();

            _overlayPanel.Visible = false;
        }

        private void OwnerBoundsChanged(object? sender, EventArgs e)
        {
            if (_overlayForm is null || _overlayForm.IsDisposed)
                return;

            _overlayForm.Bounds = _owner.Bounds;
        }

        public void Dispose()
        {
            _owner.LocationChanged -= OwnerBoundsChanged;
            _owner.SizeChanged -= OwnerBoundsChanged;

            if (_overlayForm is not null)
            {
                try { _overlayForm.Close(); } catch { }
                _overlayForm.Dispose();
                _overlayForm = null;
            }

            _overlayPanel.Visible = _overlayPanelWasVisible;
        }
    }

    internal async Task InitializeAsync()
    {
        NavigationTaskCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await MainWebView.EnsureCoreWebView2Async(WebViewEnvironment);
        MainWebView.NavigationCompleted += HandleWebViewNavigationCompleted;

        MainWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            hostName: "app",
            folderPath: Path.Combine(AppContext.BaseDirectory, "wwwroot"),
            accessKind: CoreWebView2HostResourceAccessKind.Allow
        );

        NavigateToCurrentServer();
        await NavigationTaskCompletionSource.Task;
    }

    private async Task<PingResult> GetPingResult(Server server)
    {
        PingHttpClient.Timeout = TimeSpan.FromSeconds(5);
        PingHttpClient.DefaultRequestHeaders.Clear();
        PingHttpClient.DefaultRequestHeaders.Add("Cookie", $"{Constants.AuthenticationTokenCookieName}={server.AccessToken}");

        try
        {
            var stopwatch = Stopwatch.StartNew();
            var response = await PingHttpClient.GetAsync(ServerUri.GetUri(server.IpAddress, "ping"));
            stopwatch.Stop();

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return new PingResult
                {
                    UnauthenticatedError = true,
                    RoundTripTime = stopwatch.Elapsed
                };
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                return new PingResult
                {
                    RoundTripTime = stopwatch.Elapsed
                };
            }
        }
        catch { }

        return new PingResult
        {
            ConnectionError = true,
        };
    }

    private void NavigateToCurrentServer()
    {
        if (Configuration.Servers.Count == 0)
        {
            MainWebView.Source = new Uri("https://app/NoServers.html");
            RefreshServerListMenu();
            return;
        }

        var server = Configuration.LastServerId.HasValue
            ? Configuration.Servers.First(x => x.Id == Configuration.LastServerId.Value)
            : Configuration.Servers.First();

        _ = NavigateToServerAsync(server);
        RefreshServerListMenu();
    }

    private async Task NavigateToServerAsync(Server server)
    {
        Configuration.LastServerId = server.Id;
        Configuration.SaveChanges();
        var pingResult = await GetPingResult(server);

        if (pingResult.ConnectionError)
        {
            NavigationTaskCompletionSource?.TrySetResult();
            MessageBox.Show(this, $"Unable to connect to server at {server.IpAddress}. Please check the address and your network connection.", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        else if (pingResult.UnauthenticatedError)
        {
            NavigationTaskCompletionSource?.TrySetResult();
            MessageBox.Show(this, $"Access token for server at {server.IpAddress} is invalid or expired. Please update the server configuration.", "Authentication Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var destination = ServerUri.GetUri(server.IpAddress, "client");
        var cm = MainWebView.CoreWebView2.CookieManager;

        if (!string.IsNullOrWhiteSpace(server.AccessToken))
        {
            var cookie = cm.CreateCookie("AccessToken", server.AccessToken, destination.Host, "/");
            cookie.IsHttpOnly = true;
            cookie.IsSecure = string.Equals(destination.Scheme, "https", StringComparison.OrdinalIgnoreCase);
            cm.AddOrUpdateCookie(cookie);
        }

        MainWebView.Source = destination;
        RefreshServerListMenu();
    }

    private void RefreshServerListMenu()
    {
        // Ensure the dropdown always contains:
        // 1) AddNewServerButton
        // 2) optional separator
        // 3) one item per configured server (ip address)

        ServerDropDownButton.DropDownItems.Clear();
        ServerDropDownButton.DropDownItems.Add(AddNewServerButton);

        if (Configuration.Servers.Count > 0)
        {
            ServerListSeparator = new ToolStripSeparator();
            ServerDropDownButton.DropDownItems.Add(ServerListSeparator);

            foreach (var server in Configuration.Servers)
            {
                var ip = string.IsNullOrWhiteSpace(server.IpAddress) ? "(unknown)" : server.IpAddress;
                var item = new ToolStripMenuItem(ip)
                {
                    Tag = server,
                    Checked = Configuration.LastServerId.HasValue && server.Id == Configuration.LastServerId.Value,
                    CheckOnClick = false,
                };

                item.Click += async (_, _) =>
                {
                    // If the dropdown stays open, close it before navigation.
                    ServerDropDownButton.HideDropDown();
                    await NavigateToServerAsync(server);
                };

                ServerDropDownButton.DropDownItems.Add(item);
            }
        }
        else
        {
            ServerListSeparator = null;
        }
    }

    private void HandleWebViewNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        LoadingPanel.Visible = false;

        if (!Initialized)
        {
            Initialized = true;
        }

        NavigationTaskCompletionSource?.TrySetResult();
    }

    private void ShowAddServerDialog()
    {
        NotifyAddServerClicked();

        using (new StatusStripScope(MainStatusStrip))
        using (new ModalOverlayScope(this, MainOverlayPanel, LoadingPanel))
        {
            AddServerForm.StartPosition = FormStartPosition.CenterParent;
            var result = AddServerForm.ShowDialog(this);

            if (result == DialogResult.OK)
            {
                Configuration = Configuration.Load();
                RefreshServerListMenu();
                NavigateToCurrentServer();
            }
        }
    }

    private void NotifyAddServerClicked()
    {
        try
        {
            MainWebView?.CoreWebView2?.PostWebMessageAsJson("{\"type\":\"addServerClicked\"}");
        }
        catch
        {
        }
    }

    private void ShowSettingsDialog(object sender, EventArgs e)
    {
        using (new StatusStripScope(MainStatusStrip))
        using (new ModalOverlayScope(this, MainOverlayPanel, LoadingPanel))
        {
            SettingsForm.StartPosition = FormStartPosition.CenterParent;
            SettingsForm.ShowDialog(this);
        }
    }
}

internal class PingResult
{
    public bool ConnectionError { get; set; }
    public bool UnauthenticatedError { get; set; }
    public TimeSpan? RoundTripTime { get; set; }
}