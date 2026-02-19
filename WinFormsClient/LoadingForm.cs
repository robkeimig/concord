namespace Concord.WinForms;

public partial class LoadingForm : Form
{
    public LoadingForm()
    {
        InitializeComponent();
        
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        Width = 576;
        Height = 700;
        Left = (Screen.PrimaryScreen.Bounds.Width - this.Width) / 2;
        Top = (Screen.PrimaryScreen.Bounds.Height - this.Height) / 2;
        string videoPath = Path.Combine(Application.StartupPath, "Assets", "LoadingLoop.mp4");
        LoadingLoopPlayer.enableContextMenu = false;
        LoadingLoopPlayer.fullScreen = false;
        LoadingLoopPlayer.URL = videoPath;
        LoadingLoopPlayer.settings.setMode("loop", true);
        LoadingLoopPlayer.uiMode = "none";
        LoadingLoopPlayer.Ctlcontrols.play();
        LoadingLoopPlayer.Enabled = false;

    }
}
