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

/// <summary>Internal direct-HTTPS staging increment; no public job endpoint, library writes or scans.</summary>
internal sealed class YtDlpImportRunner
{
    private static readonly string[] OutputNames = ["media.mp4", "media.mp4.part", "media.info.json", "media.mp4.ytdl"];
    private readonly YtDlpRuntime _runtime;
    private readonly string _data;
    private readonly ILogger _logger;
    private readonly ImportNetworkPolicy _policy;
    private readonly Limits _limits;
    private readonly string? _fixtureCa;

    internal YtDlpImportRunner(YtDlpRuntime runtime, IApplicationPaths paths, ILogger logger)
        : this(runtime, paths.DataPath, logger, new ImportNetworkPolicy(), new Limits(), null)
    {
    }

    internal YtDlpImportRunner(YtDlpRuntime runtime, string data, ILogger logger, ImportNetworkPolicy policy, Limits limits, string? fixtureCa)
    {
        if (limits.MediaBytes <= 0 || limits.MediaBytes > 33554432 || limits.Duration <= TimeSpan.Zero || limits.Duration > TimeSpan.FromMinutes(3))
        {
            throw new ArgumentOutOfRangeException(nameof(limits));
        }

        _runtime = runtime;
        _data = Path.GetFullPath(data);
        _logger = logger;
        _policy = policy;
        _limits = limits;
        _fixtureCa = fixtureCa;
    }

    internal async Task<StagedDownload> RunAsync(string source, CancellationToken cancellationToken, Action<long>? progress = null)
    {
        var uri = ImportNetworkPolicy.ValidateSource(source);
        if (!uri.AbsolutePath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("This staging increment accepts direct HTTPS MP4 sources only.");
        }

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        stop.CancelAfter(_limits.Duration);
        StagedDownload? staged = null;
        try
        {
            using var lease = await _runtime.AcquireAsync(stop.Token).ConfigureAwait(false);
            var parent = _data;
            foreach (var segment in new[] { "castlumora", "imports", "staging" })
            {
                parent = Path.Combine(parent, segment);
                YtDlpPlatform.PrivateDirectory(parent);
            }

            var attempt = Path.Combine(parent, Guid.NewGuid().ToString("N"));
            if (Directory.Exists(attempt))
            {
                throw new IOException("Import staging reservation already exists.");
            }

            var work = Path.Combine(attempt, "files");
            var temporary = Path.Combine(attempt, "runtime");
            staged = new StagedDownload(attempt, work, _logger);
            YtDlpPlatform.PrivateDirectory(attempt);
            YtDlpPlatform.PrivateDirectory(work);
            YtDlpPlatform.PrivateDirectory(temporary);
            var tunnel = new ImportTunnel(_policy, stop.Token);
            await using var tunnelScope = tunnel.ConfigureAwait(false);
            var info = CreateStart(lease.Executable, work, temporary, tunnel.Proxy);
            var outcome = await ImportProcess.RunAsync(info, uri.AbsoluteUri, _limits.Duration, 16384, token => MonitorAsync(work, progress, token), stop.Token).ConfigureAwait(false);
            if (outcome.ExitCode != 0)
            {
                throw new InvalidDataException(outcome.ErrorCode switch
                {
                    "certificateRejected" => "Source HTTPS certificate validation failed.",
                    "sizeLimit" => "The source exceeds the import size limit.",
                    "sourceDenied" => "The source denied access; verify source permissions.",
                    _ => "The source download failed; verify its availability and supported format."
                });
            }

            var finalBytes = CheckFiles(work);
            progress?.Invoke(finalBytes);
            if (!File.Exists(staged.MediaPath) || !File.Exists(staged.MetadataPath) || new FileInfo(staged.MediaPath).Length == 0)
            {
                throw new InvalidDataException("No complete media and metadata pair was produced; source format or size policy may reject this source.");
            }

            if (File.Exists(staged.MediaPath + ".part"))
            {
                throw new InvalidDataException("The source left an incomplete download.");
            }

            var stream = new FileStream(staged.MediaPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using var streamScope = stream.ConfigureAwait(false);
            staged.Sha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, stop.Token).ConfigureAwait(false));
            staged.Bytes = stream.Length;
            return staged;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            staged?.Dispose();
            throw new TimeoutException("The import execution reached its time limit.");
        }
        catch
        {
            staged?.Dispose();
            throw;
        }
    }

    internal ProcessStartInfo CreateStart(string executable, string work, string temporary, Uri proxy)
    {
        var info = new ProcessStartInfo(executable) { WorkingDirectory = work };
        foreach (var arg in new[]
        {
            "--ignore-config", "--no-plugin-dirs", "--no-cache-dir", "--no-playlist", "--playlist-items", "1",
            "--no-write-playlist-metafiles", "--use-extractors", "generic", "--no-js-runtimes", "--no-remote-components", "--xff", "never", "--downloader", "native", "--fixup", "never",
            "--format", "best[protocol=https]", "--quiet", "--no-progress", "--no-overwrites", "--restrict-filenames",
            "--output", "media.mp4", "--write-info-json", "--max-filesize", _limits.MediaBytes.ToString(CultureInfo.InvariantCulture),
            "--limit-rate", "1M", "--socket-timeout", "10", "--retries", "1", "--batch-file", "-"
        })
        {
            info.ArgumentList.Add(arg);
        }

        info.Environment.Clear();
        info.Environment["HOME"] = temporary;
        info.Environment["TMPDIR"] = temporary;
        info.Environment["TMP"] = temporary;
        info.Environment["TEMP"] = temporary;
        // Secrets and source query tokens stay out of process arguments: proxy in environment, URL on stdin.
        foreach (var name in new[] { "http_proxy", "https_proxy", "all_proxy", "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY" })
        {
            info.Environment[name] = proxy.AbsoluteUri;
        }

        if (OperatingSystem.IsWindows())
        {
            info.Environment["SystemRoot"] = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        }
        else
        {
            info.Environment["PATH"] = "/usr/bin:/bin";
            info.Environment["LANG"] = "C.UTF-8";
        }

        if (_fixtureCa is not null)
        {
            // Internal owned-fixture constructor only; production constructor never accepts a CA or argv.
            info.ArgumentList.Add("--compat-options");
            info.ArgumentList.Add("no-certifi");
            info.Environment["SSL_CERT_FILE"] = _fixtureCa;
        }

        return info;
    }

    private async Task MonitorAsync(string work, Action<long>? progress, CancellationToken token)
    {
        while (true)
        {
            var bytes = CheckFiles(work);
            progress?.Invoke(bytes);

            await Task.Delay(TimeSpan.FromMilliseconds(100), token).ConfigureAwait(false);
        }
    }

    private long CheckFiles(string work)
    {
        var mediaBytes = 0L;
        YtDlpPlatform.RejectLink(work);
        foreach (var path in Directory.EnumerateFileSystemEntries(work))
        {
            YtDlpPlatform.RejectLink(path);
            var name = Path.GetFileName(path);
            if (Directory.Exists(path) || !OutputNames.Contains(name, StringComparer.Ordinal))
            {
                throw new InvalidDataException("Unexpected downloader output was rejected.");
            }

            var limit = name == "media.info.json" || name.EndsWith(".ytdl", StringComparison.Ordinal) ? 2097152 : _limits.MediaBytes;
            try
            {
                var length = new FileInfo(path).Length;
                if (name is "media.mp4" or "media.mp4.part")
                {
                    mediaBytes = Math.Max(mediaBytes, length);
                }

                if (length > limit)
                {
                    throw new InvalidDataException("Import output exceeded its size limit.");
                }
            }
            catch (FileNotFoundException)
            {
                // The downloader atomically renames .part to the completed file; final checks still apply.
            }
        }

        return mediaBytes;
    }

    internal sealed record Limits(long MediaBytes = 33554432, TimeSpan? Deadline = null)
    {
        internal TimeSpan Duration => Deadline ?? TimeSpan.FromMinutes(3);
    }

    internal sealed class StagedDownload(string attempt, string work, ILogger logger) : IDisposable
    {
        internal string MediaPath { get; } = Path.Combine(work, "media.mp4");

        internal string MetadataPath { get; } = Path.Combine(work, "media.info.json");

        internal string Sha256 { get; set; } = string.Empty;

        internal long Bytes { get; set; }

        /// <inheritdoc />
        public void Dispose()
        {
            try
            {
                if (Directory.Exists(attempt))
                {
                    Directory.Delete(attempt, true);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning("Owned import staging cleanup requires retry for attempt {AttemptId}", Path.GetFileName(attempt));
            }
        }
    }
}
