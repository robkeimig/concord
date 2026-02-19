namespace Concord.WinForms;

public partial class LoadingForm : Form
{
    public LoadingForm()
    {
        InitializeComponent();
        
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        this.Width = 576;
        this.Height = 700;
        this.Left = (Screen.PrimaryScreen.Bounds.Width - this.Width) / 2;
        this.Top = (Screen.PrimaryScreen.Bounds.Height - this.Height) / 2;
        
        string videoPath = Path.Combine(Application.StartupPath, "Assets", "LoadingLoop.mp4");
        LoadingLoopPlayer.URL = videoPath;
        LoadingLoopPlayer.settings.setMode("loop", true);
        LoadingLoopPlayer.uiMode = "none";
        LoadingLoopPlayer.Ctlcontrols.play();
    }
}
