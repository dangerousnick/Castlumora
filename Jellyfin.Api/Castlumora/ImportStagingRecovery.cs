using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Api.Castlumora;

/// <summary>Reclaims marked, regenerable attempts only after the queue ownership lock is acquired.</summary>
internal static class ImportStagingRecovery
{
    internal static void Mark(string attempt, Guid job)
    {
        var marker = Path.Combine(attempt, ".castlumora-staging");
        using var file = new FileStream(marker, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(marker, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        file.Write(System.Text.Encoding.ASCII.GetBytes(job.ToString("N")));
        file.Flush(flushToDisk: true);
    }

    internal static void Recover(string data, ILogger logger)
    {
        var root = Path.Combine(data, "castlumora", "imports", "staging");
        for (var current = root; current is not null; current = Path.GetDirectoryName(current))
        {
            YtDlpPlatform.RejectLink(current);
        }

        if (!Directory.Exists(root))
        {
            return;
        }

        foreach (var attempt in Directory.EnumerateDirectories(root).Take(1000))
        {
            try
            {
                YtDlpPlatform.RejectLink(attempt);
                if (!Guid.TryParseExact(Path.GetFileName(attempt), "N", out _))
                {
                    continue;
                }

                var marker = Path.Combine(attempt, ".castlumora-staging");
                YtDlpPlatform.RejectLink(marker);
                if (!File.Exists(marker) || new FileInfo(marker).Length != 32 || !Guid.TryParseExact(File.ReadAllText(marker), "N", out _))
                {
                    continue;
                }

                var remaining = 1000;
                CheckTree(attempt, 0, ref remaining);
                Directory.Delete(attempt, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                logger.LogWarning("A marked abandoned import staging directory was retained for review ({ErrorType})", exception.GetType().Name);
            }
        }
    }

    private static void CheckTree(string path, int depth, ref int remaining)
    {
        YtDlpPlatform.RejectLink(path);
        if (depth > 8)
        {
            throw new InvalidDataException("Unusual staging directory depth; retained for review.");
        }

        var entries = Directory.EnumerateFileSystemEntries(path).Take(1001).ToArray();
        if (entries.Length > 1000 || entries.Length > remaining)
        {
            throw new InvalidDataException("Unusual staging directory contents; retained for review.");
        }

        remaining -= entries.Length;
        foreach (var entry in entries)
        {
            YtDlpPlatform.RejectLink(entry);
            if (Directory.Exists(entry))
            {
                CheckTree(entry, depth + 1, ref remaining);
            }
        }
    }
}
