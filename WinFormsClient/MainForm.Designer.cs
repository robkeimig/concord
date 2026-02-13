namespace WinFormsClient
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            MainPanel = new Panel();
            MainLayoutPanel = new TableLayoutPanel();
            MainWebView = new Microsoft.Web.WebView2.WinForms.WebView2();
            MainStatusStrip = new StatusStrip();
            ServerDropDownButton = new ToolStripDropDownButton();
            AddNewServerButton = new ToolStripMenuItem();
            LoadingPanel = new Panel();
            MainPanel.SuspendLayout();
            MainLayoutPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)MainWebView).BeginInit();
            MainStatusStrip.SuspendLayout();
            SuspendLayout();
            // 
            // MainPanel
            // 
            MainPanel.Controls.Add(MainLayoutPanel);
            MainPanel.Dock = DockStyle.Fill;
            MainPanel.Location = new Point(0, 0);
            MainPanel.Name = "MainPanel";
            MainPanel.Size = new Size(800, 450);
            MainPanel.TabIndex = 0;
            // 
            // MainLayoutPanel
            // 
            MainLayoutPanel.ColumnCount = 1;
            MainLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            MainLayoutPanel.Controls.Add(MainWebView, 0, 0);
            MainLayoutPanel.Controls.Add(MainStatusStrip, 0, 1);
            MainLayoutPanel.Dock = DockStyle.Fill;
            MainLayoutPanel.Location = new Point(0, 0);
            MainLayoutPanel.Name = "MainLayoutPanel";
            MainLayoutPanel.RowCount = 2;
            MainLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            MainLayoutPanel.RowStyles.Add(new RowStyle());
            MainLayoutPanel.Size = new Size(800, 450);
            MainLayoutPanel.TabIndex = 3;
            // 
            // MainWebView
            // 
            MainWebView.AllowExternalDrop = true;
            MainWebView.CreationProperties = null;
            MainWebView.DefaultBackgroundColor = Color.White;
            MainWebView.Dock = DockStyle.Fill;
            MainWebView.Location = new Point(3, 3);
            MainWebView.Name = "MainWebView";
            MainWebView.Size = new Size(794, 422);
            MainWebView.TabIndex = 0;
            MainWebView.ZoomFactor = 1D;
            // 
            // MainStatusStrip
            // 
            MainStatusStrip.Items.AddRange(new ToolStripItem[] { ServerDropDownButton });
            MainStatusStrip.Location = new Point(0, 428);
            MainStatusStrip.Name = "MainStatusStrip";
            MainStatusStrip.Size = new Size(800, 22);
            MainStatusStrip.TabIndex = 1;
            MainStatusStrip.Text = "statusStrip1";
            // 
            // ServerDropDownButton
            // 
            ServerDropDownButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
            ServerDropDownButton.DropDownItems.AddRange(new ToolStripItem[] { AddNewServerButton });
            ServerDropDownButton.ImageTransparentColor = Color.Magenta;
            ServerDropDownButton.Name = "ServerDropDownButton";
            ServerDropDownButton.Size = new Size(73, 20);
            ServerDropDownButton.Text = "Server List";
            // 
            // AddNewServerButton
            // 
            AddNewServerButton.Name = "AddNewServerButton";
            AddNewServerButton.Size = new Size(167, 22);
            AddNewServerButton.Text = "Add New Server...";
            // 
            // LoadingPanel
            // 
            LoadingPanel.Dock = DockStyle.Fill;
            LoadingPanel.Location = new Point(0, 0);
            LoadingPanel.Name = "LoadingPanel";
            LoadingPanel.Size = new Size(800, 450);
            LoadingPanel.TabIndex = 1;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(MainPanel);
            Controls.Add(LoadingPanel);
            Name = "MainForm";
            Text = "Concord";
            MainPanel.ResumeLayout(false);
            MainLayoutPanel.ResumeLayout(false);
            MainLayoutPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)MainWebView).EndInit();
            MainStatusStrip.ResumeLayout(false);
            MainStatusStrip.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private Panel MainPanel;
        private TableLayoutPanel MainLayoutPanel;
        private Microsoft.Web.WebView2.WinForms.WebView2 MainWebView;
        private StatusStrip MainStatusStrip;
        private ToolStripDropDownButton ServerDropDownButton;
        private ToolStripMenuItem AddNewServerButton;
        private Panel LoadingPanel;
    }
}
