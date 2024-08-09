namespace ModSync.Updater
{
    partial class ProgressForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ProgressForm));
            this.StatusText = new System.Windows.Forms.Label();
            this.ProgressBar = new System.Windows.Forms.ProgressBar();
            this.VersionLabel = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // StatusText
            // 
            this.StatusText.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.StatusText.BackColor = System.Drawing.Color.Transparent;
            this.StatusText.Font = new System.Drawing.Font("Segoe UI", 15.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.StatusText.ForeColor = System.Drawing.Color.White;
            this.StatusText.Location = new System.Drawing.Point(12, 63);
            this.StatusText.Name = "StatusText";
            this.StatusText.Size = new System.Drawing.Size(326, 37);
            this.StatusText.TabIndex = 4;
            this.StatusText.Text = "Test Text";
            this.StatusText.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            // 
            // ProgressBar
            // 
            this.ProgressBar.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.ProgressBar.Location = new System.Drawing.Point(12, 103);
            this.ProgressBar.MarqueeAnimationSpeed = 30;
            this.ProgressBar.Name = "ProgressBar";
            this.ProgressBar.Size = new System.Drawing.Size(326, 10);
            this.ProgressBar.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
            this.ProgressBar.TabIndex = 5;
            // 
            // VersionLabel
            // 
            this.VersionLabel.BackColor = System.Drawing.Color.Transparent;
            this.VersionLabel.ForeColor = System.Drawing.Color.White;
            this.VersionLabel.Location = new System.Drawing.Point(245, 8);
            this.VersionLabel.Name = "VersionLabel";
            this.VersionLabel.Size = new System.Drawing.Size(100, 23);
            this.VersionLabel.TabIndex = 6;
            this.VersionLabel.Text = "v0.0.0";
            this.VersionLabel.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // ProgressForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(73)))), ((int)(((byte)(98)))));
            this.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("$this.BackgroundImage")));
            this.ClientSize = new System.Drawing.Size(350, 125);
            this.Controls.Add(this.VersionLabel);
            this.Controls.Add(this.ProgressBar);
            this.Controls.Add(this.StatusText);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Location = new System.Drawing.Point(15, 15);
            this.MaximizeBox = false;
            this.MaximumSize = new System.Drawing.Size(350, 125);
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(350, 125);
            this.Name = "ProgressForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.TopMost = true;
            this.Load += new System.EventHandler(this.ProgressForm_Load);
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.Label VersionLabel;

        private System.Windows.Forms.ProgressBar ProgressBar;

        private System.Windows.Forms.Label StatusText;

        #endregion
    }
}