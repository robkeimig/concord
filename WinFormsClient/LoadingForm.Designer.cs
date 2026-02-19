namespace Concord.WinForms
{
    partial class LoadingForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(LoadingForm));
            LoadingLoopPlayer = new AxWMPLib.AxWindowsMediaPlayer();
            ((System.ComponentModel.ISupportInitialize)LoadingLoopPlayer).BeginInit();
            SuspendLayout();
            // 
            // LoadingLoopPlayer
            // 
            LoadingLoopPlayer.Dock = DockStyle.Fill;
            LoadingLoopPlayer.Enabled = true;
            LoadingLoopPlayer.Location = new Point(0, 0);
            LoadingLoopPlayer.Name = "LoadingLoopPlayer";
            LoadingLoopPlayer.OcxState = (AxHost.State)resources.GetObject("LoadingLoopPlayer.OcxState");
            LoadingLoopPlayer.Size = new Size(800, 450);
            LoadingLoopPlayer.TabIndex = 0;
            // 
            // LoadingForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(LoadingLoopPlayer);
            Name = "LoadingForm";
            Text = "LoadingForm";
            ((System.ComponentModel.ISupportInitialize)LoadingLoopPlayer).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private AxWMPLib.AxWindowsMediaPlayer LoadingLoopPlayer;
    }
}