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
            MainWebView = new Microsoft.Web.WebView2.WinForms.WebView2();
            ((System.ComponentModel.ISupportInitialize)MainWebView).BeginInit();
            SuspendLayout();
            // 
            // MainWebView
            // 
            MainWebView.AllowExternalDrop = true;
            MainWebView.CreationProperties = null;
            MainWebView.DefaultBackgroundColor = Color.White;
            MainWebView.Dock = DockStyle.Fill;
            MainWebView.Location = new Point(0, 0);
            MainWebView.Name = "MainWebView";
            MainWebView.Size = new Size(800, 450);
            MainWebView.TabIndex = 0;
            MainWebView.ZoomFactor = 1D;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(MainWebView);
            Name = "MainForm";
            Text = "Concord";
            ((System.ComponentModel.ISupportInitialize)MainWebView).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private Microsoft.Web.WebView2.WinForms.WebView2 MainWebView;
    }
}
