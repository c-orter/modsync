using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace ModSync.Updater
{
    internal static class Logger
    {
        public static void Log(string message)
        {
            Console.WriteLine(message);
            File.AppendAllText(Program.LOG_FILE, message + Environment.NewLine);
        }
    }

    internal static class Program
    {
        private static readonly string MODSYNC_DIR = Path.Combine(Directory.GetCurrentDirectory(), "ModSync_Data");
        public static readonly string UPDATE_DIR = Path.Combine(MODSYNC_DIR, "PendingUpdates");
        public static readonly string REMOVED_FILES_PATH = Path.Combine(MODSYNC_DIR, "RemovedFiles.json");
        public static readonly string LOG_FILE = Path.Combine(MODSYNC_DIR, "ModSync.log");

        private static void StartGraphical(int tarkovPid)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ProgressForm(tarkovPid));
        }

        private static void StartSilent(int tarkovPid)
        {
            for (var i = 0; ; i++)
            {
                try
                {
                    Process.GetProcessById(tarkovPid);
                    Logger.Log($"[Corter-ModSync Updater]: Tarkov is still running. Waiting ({i})...");
                    Thread.Sleep(100);
                }
                catch
                {
                    break;
                }
            }

            Logger.Log("[Corter-ModSync Updater]: Copying updated files...");

            try
            {
                Updater.ReplaceUpdatedFiles();
            }
            catch
            {
                Logger.Log("[Corter-ModSync Updater]: Error while attempting to copy updated files.");
                throw;
            }

            Logger.Log("[Corter-ModSync Updater]: Deleting removed files...");

            try
            {
                Updater.DeleteRemovedFiles();
            }
            catch
            {
                Logger.Log("[Corter-ModSync Updater]: Error while attempting to delete removed files.");
                throw;
            }

            Logger.Log("[Corter-ModSync Updater]: Done!");
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        public static void Main(string[] args)
        {
#pragma warning disable CS0618 // Need to access JSON dll from Tarkov files
            AppDomain.CurrentDomain.AppendPrivatePath(Path.Combine(Directory.GetCurrentDirectory(), "EscapeFromTarkov_Data", "Managed"));
#pragma warning restore CS0618
            if (File.Exists(Program.LOG_FILE))
                File.Delete(LOG_FILE);

            File.Create(Program.LOG_FILE).Dispose();

            var silent = args.Skip(1).Contains("--silent");
            var pidArg = args.Last();

            if (!int.TryParse(pidArg, out var tarkovPid))
            {
                Logger.Log("[Corter-ModSync Updater]: Error: Tarkov PID argument is not a valid integer.");
                Logger.Log("Usage: ModSync.Updater.exe [--silent] <Tarkov PID>");
                if (!silent)
                    MessageBox.Show(
                        @"Error: Tarkov PID argument is not a valid integer.",
                        @"Error running updater",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
            }

            if (!Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "ModSync_Data")))
            {
                Logger.Log(@"[Corter-ModSync Updater]: No update needed. Exiting...");
                return;
            }

            if (silent)
            {
                Logger.Log(@"[Corter-ModSync Updater]: Running silently.");
                StartSilent(tarkovPid);
            }
            else
            {
                StartGraphical(tarkovPid);
            }
        }
    }
}
