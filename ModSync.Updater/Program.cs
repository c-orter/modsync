using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace ModSync.Updater;

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
        if (args.Length == 0 || args.All(arg => arg.StartsWith("--")))
        {
            Console.WriteLine("Usage: ModSync.Updater.exe [--silent] <Tarkov PID>");

            if (!args.Contains("--silent"))
                MessageBox.Show(@"Usage: ModSync.Updater.exe [--silent] <Tarkov PID>", @"Updater usage", MessageBoxButtons.OK, MessageBoxIcon.Warning);

            return;
        }

        if (!File.Exists("EscapeFromTarkov.exe"))
        {
            Console.WriteLine("[Corter-ModSync Updater]: Error: EscapeFromTarkov.exe not found. Make sure you are running the updater from your SPT folder!");

            if (!args.Contains("--silent"))
                MessageBox.Show(
                    @"Error: EscapeFromTarkov.exe not found. Make sure you are running the updater from your SPT folder!",
                    @"Error running updater",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );

            return;
        }

        var options = args.Where(arg => arg.StartsWith("--")).ToList();
        var positional = args.Except(options);

        var silent = options.Contains("--silent");
        var pidArg = positional.Last();

        if (!Directory.Exists(MODSYNC_DIR))
        {
            Console.WriteLine("[Corter-ModSync Updater]: Error: ModSync_Data directory not found. Ensure you've run the BepInEx plugin first!");
            if (!silent)
                MessageBox.Show(
                    @"Error: ModSync_Data directory not found. Ensure you've run the BepInEx plugin first!",
                    @"Error running updater",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );

            return;
        }

        if (File.Exists(LOG_FILE))
            File.Delete(LOG_FILE);

        File.Create(LOG_FILE).Dispose();

        if (!int.TryParse(pidArg, out var tarkovPid))
        {
            Logger.Log("[Corter-ModSync Updater]: Error: Tarkov PID argument is not a valid integer.");
            Logger.Log("Usage: ModSync.Updater.exe [--silent] <Tarkov PID>");
            if (!silent)
                MessageBox.Show(@"Error: Tarkov PID argument is not a valid integer.", @"Error running updater", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
