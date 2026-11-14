using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Api.Castlumora;

/// <summary>Owned single-video downloads; only local, inspected streams reach FFmpeg.</summary>
internal sealed class YouTubeDownload(YtDlpRuntime runtime, IApplicationPaths paths, string encoder, ILogger logger)
{
    internal async Task<YtDlpImportRunner.StagedDownload> RunAsync(ImportQueue.Job job, Action<long> progress, CancellationToken token)
    {
        var options = job.YouTube ?? throw new InvalidDataException("A confirmed YouTube resource snapshot is required.");
        options.Validate(job.Source);
        var node = await YouTubeNodeRuntime.VerifyAsync(paths.ProgramSystemPath, token).ConfigureAwait(false);
        using var lease = await runtime.AcquireAsync(token).ConfigureAwait(false);
        var parent = Path.Combine(paths.DataPath, "castlumora", "imports", "staging");
        for (var current = parent; current is not null; current = Path.GetDirectoryName(current))
        {
            YtDlpPlatform.RejectLink(current);
        }

        YtDlpPlatform.PrivateDirectory(parent);
        if (new DriveInfo(parent).AvailableFreeSpace < checked((options.MaximumBytes * 4) + 134217728))
        {
            throw new IOException("Insufficient staging capacity for the selected import budget and reserve.");
        }

        var attempt = Path.Combine(parent, Guid.NewGuid().ToString("N"));
        var work = Path.Combine(attempt, "files");
        var temporary = Path.Combine(attempt, "runtime");
        YtDlpPlatform.PrivateDirectory(attempt);
        ImportStagingRecovery.Mark(attempt, job.Id);
        YtDlpPlatform.PrivateDirectory(work);
        YtDlpPlatform.PrivateDirectory(temporary);
        var staged = new YtDlpImportRunner.StagedDownload(attempt, work, logger);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        stop.CancelAfter(TimeSpan.FromMinutes(10));
        try
        {
            await using var tunnel = new ImportTunnel(new ImportNetworkPolicy(), stop.Token, (options.MaximumBytes * 2) + 33554432);
            const string videoName = "video.mp4";
            await DownloadAsync(options.VideoFormat, videoName).ConfigureAwait(false);
            if (options.AudioFormat is not null)
            {
                await DownloadAsync(options.AudioFormat, "audio.m4a").ConfigureAwait(false);
                if (new FileInfo(Path.Combine(work, videoName)).Length + new FileInfo(Path.Combine(work, "audio.m4a")).Length > options.MaximumBytes)
                {
                    throw ImportFailure.Download("sizeLimit");
                }
            }

            await MuxAsync(Path.Combine(work, videoName), options.AudioFormat is null ? null : Path.Combine(work, "audio.m4a"), staged.MediaPath, encoder, options.MaximumBytes, stop.Token).ConfigureAwait(false);
            CheckFiles(work, options.MaximumBytes, progress);
            await using var file = new FileStream(staged.MediaPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
            staged.Bytes = file.Length;
            if (staged.Bytes <= 0 || staged.Bytes > options.MaximumBytes)
            {
                throw ImportFailure.Download("sizeLimit");
            }

            staged.Sha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(file, stop.Token).ConfigureAwait(false));
            return staged;

            async Task DownloadAsync(string format, string output)
            {
                var start = CreateStart(lease.Executable, node, work, temporary, tunnel.Proxy, format, output, options.MaximumBytes);
                var result = await ImportProcess.RunAsync(start, job.Source, TimeSpan.FromMinutes(10), 16384, ct => MonitorAsync(work, options.MaximumBytes, progress, ct), stop.Token).ConfigureAwait(false);
                if (result.ExitCode != 0)
                {
                    throw ImportFailure.Download(result.ErrorCode == "downloadFailed" && tunnel.HasConnectionFailure ? "sourceNetwork" : result.ErrorCode);
                }

                var media = Path.Combine(work, output);
                var info = Path.ChangeExtension(media, ".info.json");
                CheckFiles(work, options.MaximumBytes, progress);
                if (!File.Exists(media) || !File.Exists(info) || File.Exists(media + ".part"))
                {
                    throw ImportFailure.Download("incompleteOutput");
                }

                var metadata = YouTubeMetadataPreview.Parse(await File.ReadAllTextAsync(info, stop.Token).ConfigureAwait(false), YouTubeVideoSource.Parse(job.Source));
                if (metadata.ChannelId != options.ChannelId)
                {
                    throw new InvalidDataException("The source creator changed since confirmation; review a new preview.");
                }

                ImportMediaInspection.CheckContainer(media, options.MaximumBytes, requireVideo: output != "audio.m4a", allowFragments: true);
            }
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            staged.Dispose();
            throw new TimeoutException("The YouTube import reached its execution deadline.");
        }
        catch
        {
            staged.Dispose();
            throw;
        }
    }

    internal static ProcessStartInfo CreateStart(string executable, string node, string work, string temporary, Uri proxy, string format, string output, long maximumBytes)
    {
        if (string.IsNullOrEmpty(format) || format.Length > 16 || !format.All(char.IsAsciiDigit)
            || output is not ("media.mp4" or "video.mp4" or "audio.m4a") || maximumBytes is <= 0 or > YouTubeImportOptions.AbsoluteMaximumBytes)
        {
            throw new InvalidDataException("Invalid fixed downloader format or output.");
        }

        var start = YouTubeMetadataPreview.CreateStart(executable, node, temporary, proxy);
        start.WorkingDirectory = work;
        start.ArgumentList.Remove("--skip-download");
        start.ArgumentList.Remove("--dump-single-json");
        foreach (var argument in new[]
        {
            "--downloader", "native", "--fixup", "never", "--no-progress", "--no-overwrites", "--restrict-filenames",
            "--format", format, "--output", output, "--write-info-json", "--max-filesize", maximumBytes.ToString(CultureInfo.InvariantCulture),
            "--limit-rate", "4M", "--concurrent-fragments", "1"
        })
        {
            start.ArgumentList.Add(argument);
        }

        return start;
    }

    internal static async Task MuxAsync(string video, string? audio, string output, string encoder, long maximumBytes, CancellationToken token)
    {
        // The existing configured encoder is resolved by the publication service;
        // no executable name is discovered from an ambient PATH.
        if (!Path.IsPathFullyQualified(encoder) || !File.Exists(encoder))
        {
            throw new InvalidOperationException("Configured media encoder required.");
        }

        var start = new ProcessStartInfo(encoder) { WorkingDirectory = Path.GetDirectoryName(output)! };
        foreach (var argument in new[] { "-hide_banner", "-v", "error", "-nostdin", "-n", "-max_alloc", "16777216", "-threads", "1" })
        {
            start.ArgumentList.Add(argument);
        }

        foreach (var input in audio is null ? new[] { video } : new[] { video, audio })
        {
            ImportMediaInspection.CheckContainer(input, maximumBytes, input == video, allowFragments: true);
            foreach (var argument in new[] { "-protocol_whitelist", "file", "-format_whitelist", "mov", "-enable_drefs", "0", "-use_absolute_path", "0", "-i", input })
            {
                start.ArgumentList.Add(argument);
            }
        }

        foreach (var argument in new[] { "-map", "0:v:0", "-map", audio is null ? "0:a:0?" : "1:a:0", "-c", "copy", "-map_metadata", "-1", "-map_chapters", "-1", "-movflags", "+faststart", output })
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment.Clear();
        var result = await ImportProcess.RunAsync(start, string.Empty, TimeSpan.FromMinutes(2), 16384, ct => MonitorAsync(Path.GetDirectoryName(output)!, maximumBytes, _ => { }, ct), token).ConfigureAwait(false);
        if (result.ExitCode != 0 || !File.Exists(output) || new FileInfo(output).Length > maximumBytes)
        {
            throw new InvalidDataException("Local audio/video muxing failed its bounded media policy.");
        }
    }

    private static async Task MonitorAsync(string work, long maximumBytes, Action<long> progress, CancellationToken token)
    {
        while (true)
        {
            CheckFiles(work, maximumBytes, progress);
            await Task.Delay(100, token).ConfigureAwait(false);
        }
    }

    private static void CheckFiles(string work, long maximumBytes, Action<long> progress)
    {
        long total = 0;
        long media = 0;
        foreach (var path in Directory.EnumerateFileSystemEntries(work))
        {
            YtDlpPlatform.RejectLink(path);
            var name = Path.GetFileName(path);
            var diagnostic = name is "media.info.json" or "video.info.json" or "audio.info.json";
            var content = name is "media.mp4" or "video.mp4" or "audio.m4a" or "media.mp4.part" or "video.mp4.part" or "audio.m4a.part";
            if (Directory.Exists(path) || (!diagnostic && !content && name is not ("media.mp4.ytdl" or "video.mp4.ytdl" or "audio.m4a.ytdl")))
            {
                throw ImportFailure.Download("unexpectedOutput");
            }

            try
            {
                var bytes = new FileInfo(path).Length;
                if (bytes > (content ? maximumBytes : 2097152))
                {
                    throw ImportFailure.Download("sizeLimit");
                }

                total += bytes;
                if (content)
                {
                    media += bytes;
                }
            }
            catch (FileNotFoundException)
            {
                // Native downloader may atomically rename its owned .part file.
            }
        }

        if (total > (maximumBytes * 2) + 6291456)
        {
            throw ImportFailure.Download("sizeLimit");
        }

        progress(Math.Min(media, maximumBytes));
    }
}
