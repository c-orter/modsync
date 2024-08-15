using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SPT.Common.Http;
using SPT.Common.Utils;

namespace ModSync;

public class Server(Version pluginVersion)
{
    private async Task<string> GetJson(string path)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("modsync-version", pluginVersion.ToString());
        var json = await client.GetStringAsync($@"{RequestHandler.Host}{path}");
        return json;
    }

    public async Task DownloadFile(string file, string downloadDir, SemaphoreSlim limiter, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return;

        var downloadPath = Path.Combine(downloadDir, file);
        VFS.CreateDirectory(downloadPath.GetDirectory());

        var retryCount = 0;

        await limiter.WaitAsync(cancellationToken);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var client = new HttpClient();
                using var responseStream = await client.GetStreamAsync($@"{RequestHandler.Host}/modsync/fetch/{file}");
                using var fileStream = new FileStream(downloadPath, FileMode.Create);

                await responseStream.CopyToAsync(fileStream, (int)responseStream.Length, cancellationToken);
                limiter.Release();
                return;
            }
            catch (Exception e)
            {
                if (e is TaskCanceledException && cancellationToken.IsCancellationRequested)
                    throw;

                if (retryCount < 5)
                {
                    Plugin.Logger.LogError($"Failed to download '{file}'. Retrying ({retryCount + 1}/5)...");
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
        return Json.Deserialize<string>(Task.Run(() => GetJson($"/modsync/version")).Result);
    }

    public List<SyncPath> GetModSyncPaths()
    {
        return Json.Deserialize<List<SyncPath>>(Task.Run(() => GetJson($"/modsync/paths")).Result);
    }

    public Dictionary<string, Dictionary<string, ModFile>> GetRemoteModFileHashes()
    {
        return Json.Deserialize<Dictionary<string, Dictionary<string, ModFile>>>(Task.Run(() => GetJson($"/modsync/hashes")).Result)
            .ToDictionary(
                item => item.Key,
                item => item.Value.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase
            );
    }
}
