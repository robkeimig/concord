using Microsoft.Web.WebView2.Core;
using WinFormsClient;

namespace Concord.WinForms;

public class ConcordContext : ApplicationContext
{
    Configuration Configuration;
    LoadingForm LoadingForm;
    CoreWebView2Environment WebViewEnvironment;
    MainForm MainForm;
    SettingsForm SettingsForm;
    AddServerForm AddServerForm;

    public ConcordContext()
    {
        InitializeAsync();
    }


    private async void InitializeAsync()
    {
        LoadingForm = new LoadingForm();
        LoadingForm.FormClosed += (s, e) => ExitThread();
        LoadingForm.Show();

        WebViewEnvironment = await CoreWebView2Environment.CreateAsync(
            userDataFolder: Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ConcordWebView"
            )
        );

        Configuration = Configuration.Load();
        MainForm = new MainForm(WebViewEnvironment, Configuration);
        SettingsForm = new SettingsForm(WebViewEnvironment, Configuration);
        AddServerForm = new AddServerForm(WebViewEnvironment, Configuration);
        var mainFormInitializeTask = MainForm.InitializeAsync();
        var settingsFormInitializeTask = SettingsForm.InitializeAsync();    
        var addServerFormInitializeTask = AddServerForm.InitializeAsync();
        await Task.WhenAll(mainFormInitializeTask, settingsFormInitializeTask, addServerFormInitializeTask);
        await Task.Delay(1000);
        MainForm.Show();
        LoadingForm.Hide();
    }
}
