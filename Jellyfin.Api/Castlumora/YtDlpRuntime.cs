using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;

namespace Jellyfin.Api.Castlumora;

/// <summary>Installs only shipped, pinned yt-dlp assets and performs bounded offline health checks.</summary>
public sealed class YtDlpRuntime : IDisposable
{
    private readonly string _data;
    private readonly string _system;
    private readonly YtDlpPlatform? _platform;
    private readonly Func<string, string, string, CancellationToken, Task> _probe;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _executions;

    /// <summary>Initializes a new instance of the <see cref="YtDlpRuntime"/> class.</summary>
    /// <param name="paths">Existing server data and application paths.</param>
    public YtDlpRuntime(IApplicationPaths paths)
        : this(paths.DataPath, paths.ProgramSystemPath, YtDlpPlatform.Detect(YtDlpPlatform.CurrentId()), ProbeAsync)
    {
    }

    internal YtDlpRuntime(string data, string system, YtDlpPlatform? platform, Func<string, string, string, CancellationToken, Task> probe)
    {
        _data = data;
        _system = system;
        _platform = platform;
        _probe = probe;
    }

    /// <summary>Gets integrity and executable health; no network request or automatic install.</summary>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Administrator runtime state.</returns>
    public async Task<JsonObject> StatusAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await StateAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Atomically installs the hash-verified bundled asset after a successful health check.</summary>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Ready runtime state.</returns>
    public async Task<JsonObject> InstallAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _executions) != 0)
            {
                throw new BusyException();
            }

            var platform = _platform ?? throw new NotSupportedException("This preview has managed yt-dlp assets for Linux x64, macOS x64/ARM64 and Windows x64.");
            var bundle = BundlePath();
            await VerifyAsync(bundle, platform, cancellationToken).ConfigureAwait(false);
            var destination = ManagedPath(true);
            var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                // A partial/canceled install cannot replace the current executable.
                var source = new FileStream(bundle, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
                await using (source.ConfigureAwait(false))
                {
                    var target = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous);
                    await using (target.ConfigureAwait(false))
                    {
                        var buffer = new byte[65536];
                        long copied = 0;
                        while (true)
                        {
                            var count = await source.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                            if (count == 0)
                            {
                                break;
                            }

                            copied += count;
                            if (copied > platform.Size)
                            {
                                throw new InvalidDataException("Bundled executable exceeded its pinned size during installation.");
                            }

                            await target.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
                        }

                        await target.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }
                }

                await VerifyAsync(temporary, platform, cancellationToken).ConfigureAwait(false);
                YtDlpPlatform.Executable(temporary);
                await _probe(temporary, Path.GetDirectoryName(destination)!, platform.Version, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                YtDlpPlatform.RejectLink(destination);
                File.Move(temporary, destination, true);
                return State("ready", "Pinned yt-dlp installed and verified.", true);
            }
            finally
            {
                File.Delete(temporary);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    internal async Task<ExecutionLease> AcquireAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var platform = _platform ?? throw new NotSupportedException("Managed runtime unavailable on this platform.");
            var path = ManagedPath(false);
            await VerifyAsync(path, platform, cancellationToken).ConfigureAwait(false);
            await _probe(path, Path.GetDirectoryName(path)!, platform.Version, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _executions);
            return new ExecutionLease(path, () => Interlocked.Decrement(ref _executions));
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();

    private async Task<JsonObject> StateAsync(CancellationToken cancellationToken)
    {
        if (_platform is null)
        {
            return State("unsupportedPlatform", "Managed runtime is currently packaged for Linux x64, macOS x64/ARM64 and Windows x64.", false);
        }

        try
        {
            var path = ManagedPath(false);
            if (!File.Exists(path))
            {
                return State("notInstalled", "Install the pinned bundled runtime to prepare media importing.", false);
            }

            await VerifyAsync(path, _platform, cancellationToken).ConfigureAwait(false);
            await _probe(path, Path.GetDirectoryName(path)!, _platform.Version, cancellationToken).ConfigureAwait(false);
            return State("ready", "Pinned executable integrity and version verified.", true);
        }
        catch (InvalidDataException exception)
        {
            return State("verificationFailed", exception.Message, false);
        }
        catch (InvalidOperationException)
        {
            return State("healthFailed", "The executable did not complete its version check within the time limit.", false);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return State("launchFailed", "The executable could not start. Check OS dependencies and executable filesystem permissions.", false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return State("checkFailed", "Runtime could not be verified. Reinstall from the bundled release; inspect OS/runtime dependencies if it cannot start.", false);
        }
    }

    private JsonObject State(string state, string message, bool ready)
    {
        var release = YtDlpPlatform.Release();
        var available = false;
        if (_platform is not null)
        {
            try
            {
                available = File.Exists(BundlePath());
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                // The presence flag never substitutes for verification during installation.
            }
        }

        return new JsonObject
        {
            ["state"] = state,
            ["message"] = message,
            ["ready"] = ready,
            ["platform"] = _platform?.Id ?? YtDlpPlatform.CurrentId(),
            ["expectedVersion"] = _platform?.Version ?? release["version"]!.GetValue<string>(),
            ["expectedSha256"] = _platform?.Sha256,
            ["bundleAvailable"] = available,
            ["supported"] = _platform is not null,
            ["downloadJobsAvailable"] = false,
            ["activeExecutions"] = Volatile.Read(ref _executions),
            ["releaseUrl"] = release["releaseUrl"]!.GetValue<string>()
        };
    }

    private string ManagedPath(bool create)
    {
        var path = _data;
        foreach (var segment in new[] { "castlumora", "tools", "yt-dlp", _platform!.Version, _platform.Id })
        {
            path = Path.Combine(path, segment);
            if (create)
            {
                YtDlpPlatform.PrivateDirectory(path);
            }
            else
            {
                YtDlpPlatform.RejectLink(path);
            }
        }

        path = Path.Combine(path, _platform.FileName);
        YtDlpPlatform.RejectLink(path);
        return path;
    }

    private string BundlePath()
    {
        var path = _system;
        foreach (var segment in new[] { "tools", "yt-dlp", _platform!.Version, _platform.Id, _platform.FileName })
        {
            path = Path.Combine(path, segment);
            YtDlpPlatform.RejectLink(path);
        }

        return path;
    }

    private static async Task VerifyAsync(string path, YtDlpPlatform platform, CancellationToken cancellationToken)
    {
        YtDlpPlatform.RejectLink(path);
        if (!File.Exists(path) || new FileInfo(path).Length != platform.Size)
        {
            throw new InvalidDataException("Bundled or installed runtime is missing or has an unexpected size.");
        }

        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var streamScope = stream.ConfigureAwait(false);
        var actual = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
        if (!string.Equals(actual, platform.Sha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Managed executable does not match the reviewed release checksum.");
        }
    }

    internal static async Task ProbeAsync(string executable, string directory, string version, CancellationToken cancellationToken)
    {
        var temporary = Path.Combine(directory, "health-" + Guid.NewGuid().ToString("N"));
        YtDlpPlatform.PrivateDirectory(temporary);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = temporary
        };
        process.StartInfo.ArgumentList.Add("--ignore-config");
        process.StartInfo.ArgumentList.Add("--no-plugin-dirs");
        process.StartInfo.ArgumentList.Add("--version");
        process.StartInfo.Environment.Clear();
        process.StartInfo.Environment["TMPDIR"] = temporary;
        process.StartInfo.Environment["TMP"] = temporary;
        process.StartInfo.Environment["TEMP"] = temporary;
        if (OperatingSystem.IsWindows())
        {
            // Windows loader/runtime discovery needs the actual Windows directory, not a Unix PATH.
            process.StartInfo.Environment["SystemRoot"] = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        }
        else
        {
            process.StartInfo.Environment["PATH"] = "/usr/bin:/bin";
            process.StartInfo.Environment["LANG"] = "C.UTF-8";
        }

        var started = false;
        Task[] work = [];
        try
        {
            started = process.Start();
            var output = ReadBoundedAsync(process.StandardOutput, timeout.Token);
            var error = ReadBoundedAsync(process.StandardError, timeout.Token);
            work = [output, error, process.WaitForExitAsync(timeout.Token)];
            var pending = new List<Task>(work);
            while (pending.Count > 0)
            {
                var completed = await Task.WhenAny(pending).WaitAsync(timeout.Token).ConfigureAwait(false);
                pending.Remove(completed);
                // Abort immediately on oversized output instead of waiting on a blocked child.
                await completed.ConfigureAwait(false);
            }

            if (process.ExitCode != 0 || !string.Equals((await output.ConfigureAwait(false)).Trim(), version, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Executable health check did not report the expected version.");
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("Executable health check timed out.");
        }
        finally
        {
            await timeout.CancelAsync().ConfigureAwait(false);
            if (started)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }

                    await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None).ConfigureAwait(false);
                }
                catch (InvalidOperationException)
                {
                    // The owned process may exit between HasExited and Kill.
                }
            }

            try
            {
                await Task.WhenAll(work).WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or OperationCanceledException or TimeoutException)
            {
                // Observe canceled/failed readers while retaining the original failure.
            }

            try
            {
                Directory.Delete(temporary, true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Owned extraction files can briefly remain locked by OS security scanning.
            }
        }
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var buffer = new char[1024];
        var result = new StringBuilder();
        while (true)
        {
            var count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                return result.ToString();
            }

            if (result.Length + count > 4096)
            {
                throw new InvalidDataException("Executable health output exceeded its limit.");
            }

            result.Append(buffer, 0, count);
        }
    }

    internal sealed class ExecutionLease(string executable, Action release) : IDisposable
    {
        private Action? _release = release;

        internal string Executable { get; } = executable;

        /// <inheritdoc />
        public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
    }

    internal sealed class BusyException() : InvalidOperationException("Managed runtime cannot be replaced while an import execution holds a lease.");
}
