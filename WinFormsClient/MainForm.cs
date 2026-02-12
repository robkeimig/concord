namespace WinFormsClient
{
    public partial class MainForm : Form
    {
        private readonly Configuration Configuration;

        public MainForm()
        {
            InitializeComponent();
            Configuration = Configuration.Load();
        }
    }
}
