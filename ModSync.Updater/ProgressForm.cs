using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ModSync.Updater
{
    public partial class ProgressForm : Form
    {
        private readonly int tarkovPid;

        public ProgressForm(int tarkovPid)
        {
            this.tarkovPid = tarkovPid;

            InitializeComponent();
        }

        private async void ProgressForm_Load(object sender, EventArgs _)
        {
            StatusText.Text = @"Waiting while Tarkov closes...";
            while (true)
            {
                try
                {
                    Process.GetProcessById(tarkovPid);
                    Logger.Log("[Corter-ModSync Updater]: Tarkov is still running. Waiting...");
                    await Task.Delay(100);
                }
                catch
                {
                    break;
                }
            }

            var stopwatch = Stopwatch.StartNew();
            StatusText.Text = @"Copying updated files...";
            try
            {
                await Task.Run(Updater.ReplaceUpdatedFiles);
            }
            catch (Exception e)
            {
                Logger.Log($"Error while attempting to copy updated files: {e.Message}");
                Logger.Log(e.StackTrace);

                ProgressBar.Style = ProgressBarStyle.Blocks;
                MessageBox.Show(
                    $"An error occurred while attempting to copy updated files:\n\n{e.Message}\n\nCheck ModSync_Data/ModSync.log for more details.",
                    @"Error copying files",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                Application.Exit();
            }

            StatusText.Text = @"Deleting removed files...";
            try
            {
                await Task.Run(Updater.DeleteRemovedFiles);
            }
            catch (Exception e)
            {
                Logger.Log($"Error while attempting to delete removed files: {e.Message}");
                Logger.Log(e.StackTrace);

                ProgressBar.Style = ProgressBarStyle.Blocks;
                MessageBox.Show(
                    $"An error occurred while attempting to delete removed files:\n\n{e.Message}\n\nCheck ModSync_Data/ModSync.log for more details.",
                    @"Error deleting files",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                Application.Exit();
            }

            StatusText.Text = @"Update complete!";
            ProgressBar.Style = ProgressBarStyle.Continuous;
            ProgressBar.Value = 100;
            stopwatch.Stop();

            if (stopwatch.ElapsedMilliseconds < 2000)
                await Task.Delay(2000 - (int)stopwatch.ElapsedMilliseconds);

            Application.Exit();
        }
    }
}
