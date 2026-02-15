namespace Concord.WinForms
{
    partial class SettingsForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            LoadingPanel = new Panel();
            LoadingLayoutPanel = new TableLayoutPanel();
            LoadingProgressBar = new ProgressBar();
            LoadingLabel = new Label();
            SettingsWebView = new Microsoft.Web.WebView2.WinForms.WebView2();
            LoadingPanel.SuspendLayout();
            LoadingLayoutPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)SettingsWebView).BeginInit();
            SuspendLayout();
            // 
            // LoadingPanel
            // 
            LoadingPanel.BackColor = Color.FromArgb(32, 32, 32);
            LoadingPanel.Controls.Add(LoadingLayoutPanel);
            LoadingPanel.Dock = DockStyle.Fill;
            LoadingPanel.Location = new Point(0, 0);
            LoadingPanel.Name = "LoadingPanel";
            LoadingPanel.Size = new Size(800, 450);
            LoadingPanel.TabIndex = 2;
            // 
            // LoadingLayoutPanel
            // 
            LoadingLayoutPanel.ColumnCount = 1;
            LoadingLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            LoadingLayoutPanel.Controls.Add(LoadingProgressBar, 0, 1);
            LoadingLayoutPanel.Controls.Add(LoadingLabel, 0, 0);
            LoadingLayoutPanel.Dock = DockStyle.Fill;
            LoadingLayoutPanel.Location = new Point(0, 0);
            LoadingLayoutPanel.Name = "LoadingLayoutPanel";
            LoadingLayoutPanel.RowCount = 3;
            LoadingLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            LoadingLayoutPanel.RowStyles.Add(new RowStyle());
            LoadingLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            LoadingLayoutPanel.Size = new Size(800, 450);
            LoadingLayoutPanel.TabIndex = 0;
            // 
            // LoadingProgressBar
            // 
            LoadingProgressBar.Anchor = AnchorStyles.None;
            LoadingProgressBar.ForeColor = Color.DodgerBlue;
            LoadingProgressBar.Location = new Point(280, 216);
            LoadingProgressBar.MarqueeAnimationSpeed = 30;
            LoadingProgressBar.Name = "LoadingProgressBar";
            LoadingProgressBar.Size = new Size(240, 18);
            LoadingProgressBar.Style = ProgressBarStyle.Marquee;
            LoadingProgressBar.TabIndex = 0;
            // 
            // LoadingLabel
            // 
            LoadingLabel.Anchor = AnchorStyles.None;
            LoadingLabel.AutoSize = true;
            LoadingLabel.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
            LoadingLabel.ForeColor = Color.White;
            LoadingLabel.Location = new Point(358, 92);
            LoadingLabel.Name = "LoadingLabel";
            LoadingLabel.Padding = new Padding(0, 0, 0, 8);
            LoadingLabel.Size = new Size(84, 29);
            LoadingLabel.TabIndex = 1;
            LoadingLabel.Text = "Loading...";
            // 
            // SettingsWebView
            // 
            SettingsWebView.AllowExternalDrop = true;
            SettingsWebView.CreationProperties = null;
            SettingsWebView.DefaultBackgroundColor = Color.White;
            SettingsWebView.Dock = DockStyle.Fill;
            SettingsWebView.Location = new Point(0, 0);
            SettingsWebView.Name = "SettingsWebView";
            SettingsWebView.Size = new Size(800, 450);
            SettingsWebView.TabIndex = 2;
            SettingsWebView.ZoomFactor = 1D;
            // 
            // SettingsForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(SettingsWebView);
            Controls.Add(LoadingPanel);
            Name = "SettingsForm";
            Text = "SettingsForm";
            LoadingPanel.ResumeLayout(false);
            LoadingLayoutPanel.ResumeLayout(false);
            LoadingLayoutPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)SettingsWebView).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private Panel LoadingPanel;
        private TableLayoutPanel LoadingLayoutPanel;
        private ProgressBar LoadingProgressBar;
        private Label LoadingLabel;
        private Microsoft.Web.WebView2.WinForms.WebView2 SettingsWebView;
    }
}