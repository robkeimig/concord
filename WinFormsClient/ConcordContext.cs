namespace Concord.WinForms;

public class ConcordContext : ApplicationContext
{
    LoadingForm LoadingForm;

    public ConcordContext()
    {
        PresentLoadingForm();
        InitializeAsync();
    }

    private void PresentLoadingForm()
    {
        LoadingForm = new LoadingForm();
        LoadingForm.FormClosed += (s, e) => ExitThread();
        LoadingForm.Show();
    }

    private async void InitializeAsync()
    {
        //var mainForm = new MainForm();
        //MainForm = mainForm;
        //mainForm.Show();
    }
}
