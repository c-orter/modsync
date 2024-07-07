using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SPT.Common.Http;
using SPT.Common.Utils;

namespace ModSync
{
    public class Server
    {
        public async Task DownloadFile(string file, string downloadDir, SemaphoreSlim limiter, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            var downloadPath = Path.Combine(downloadDir, file);
            VFS.CreateDirectory(downloadPath.GetDirectory());

            if (file == @"BepInEx\patchers\Corter-ModSync-Patcher.dll")
                downloadPath = Path.Combine(Directory.GetCurrentDirectory(), @"BepInEx\patchers\Corter-ModSync-Patcher.dll");

            try
            {
                await limiter.WaitAsync(cancellationToken);
                using var client = new HttpClient();
                using var fileStream = new FileStream(downloadPath, FileMode.CreateNew);
                using var responseStream = await client.GetStreamAsync($@"{RequestHandler.Host}/modsync/fetch/{file}");

                await responseStream.CopyToAsync(fileStream, (int)responseStream.Length, cancellationToken);
                limiter.Release();
            }
            catch (TaskCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError($"Failed to download '{file}'. Please see the stacktrace below for details.");
                Plugin.Logger.LogError(e);
                throw;
            }
        }

        public string GetModSyncVersion()
        {
            return Json.Deserialize<string>(RequestHandler.GetJson("/modsync/version"));
        }

        public string[] GetModSyncPaths()
        {
            return Json.Deserialize<string[]>(RequestHandler.GetJson("/modsync/paths"));
        }

        public Dictionary<string, ModFile> GetRemoteModFileHashes()
        {
            return Json.Deserialize<Dictionary<string, ModFile>>(RequestHandler.GetJson("/modsync/hashes"))
                .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
        }
    }
}
