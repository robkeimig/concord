namespace WinFormsClient
{
    public partial class MainForm : Form
    {
        private readonly Configuration Configuration;

        public MainForm()
        {
            InitializeComponent();
            Configuration = Configuration.Load();

            Shown += MainForm_Shown;
        }

        private async void MainForm_Shown(object? sender, EventArgs e)
        {
            Shown -= MainForm_Shown;

            // Ensure WebView2 is initialized before navigation.
            await MainWebView.EnsureCoreWebView2Async();
            MainWebView.CoreWebView2.Navigate("http://localhost/");
        }
    }
}
