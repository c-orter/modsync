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

            var retryCount = 0;

            while (retryCount < 5 && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await limiter.WaitAsync(cancellationToken);
                    using var client = new HttpClient();
                    client.Timeout = TimeSpan.FromMinutes(5);
                    using var fileStream = new FileStream(downloadPath, FileMode.Create);
                    using var responseStream = await client.GetStreamAsync($@"{RequestHandler.Host}/modsync/fetch/{file}");

                    await responseStream.CopyToAsync(fileStream, (int)responseStream.Length, cancellationToken);
                    limiter.Release();
                    return;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    if (retryCount < 4)
                    {
                        Plugin.Logger.LogError($"Failed to download '{file}'. Retrying...");
                        Plugin.Logger.LogError(e);
                        await Task.Delay(500, cancellationToken);
                        retryCount++;
                    }
                    else
                    {
                        Plugin.Logger.LogError($"Failed to download '{file}'. Exiting...");
                        Plugin.Logger.LogError(e);
                        throw;
                    }
                }
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
