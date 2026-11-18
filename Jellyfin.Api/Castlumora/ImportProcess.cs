using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Api.Castlumora;

/// <summary>Bounded owned-process lifetime; raw downloader output is never returned or logged.</summary>
internal static class ImportProcess
{
    internal static async Task<Result> RunAsync(
        ProcessStartInfo start,
        string input,
        TimeSpan duration,
        int outputLimit,
        Func<CancellationToken, Task> monitor,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(outputLimit);

        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        start.RedirectStandardInput = true;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        stop.CancelAfter(duration);
        using var process = new Process { StartInfo = start };
        var started = false;
        Task[] work = [];
        Task? watchTask = null;
        var completedNormally = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            started = process.Start();
            var output = ReadAsync(process.StandardOutput, outputLimit, stop.Token);
            var error = ReadAsync(process.StandardError, outputLimit, stop.Token);
            var send = InputAsync(process.StandardInput, input, stop.Token);
            var exit = process.WaitForExitAsync(stop.Token);
            var watch = monitor(stop.Token);
            watchTask = watch;
            work = [output, error, send, exit, watch];
            var pending = new List<Task> { output, error, send, exit, watch };
            while (pending.Count > 1)
            {
                var completed = await Task.WhenAny(pending).WaitAsync(stop.Token).ConfigureAwait(false);
                if (completed == watch)
                {
                    await watch.ConfigureAwait(false);
                    throw new InvalidOperationException("Import monitor stopped unexpectedly.");
                }

                pending.Remove(completed);
                await completed.ConfigureAwait(false);
            }

            completedNormally = true;
            return new Result(process.ExitCode, Classify(await error.ConfigureAwait(false))) { Output = await output.ConfigureAwait(false) };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("The import execution reached its time limit.");
        }
        finally
        {
            await stop.CancelAsync().ConfigureAwait(false);
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
                    // The owned process can exit between HasExited and Kill.
                }
            }

            try
            {
                await Task.WhenAll(work).WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or OperationCanceledException or TimeoutException)
            {
                // Observe failed/canceled streams without exposing raw output or masking its cause.
            }

            if (completedNormally && watchTask is { IsFaulted: true })
            {
                await watchTask.ConfigureAwait(false);
            }
        }
    }

    private static async Task InputAsync(StreamWriter writer, string input, CancellationToken token)
    {
        if (input.Length != 0)
        {
            await writer.WriteLineAsync(input.AsMemory(), token).ConfigureAwait(false);
            await writer.FlushAsync(token).ConfigureAwait(false);
        }

        writer.Close();
    }

    private static async Task<string> ReadAsync(StreamReader reader, int limit, CancellationToken token)
    {
        var result = new StringBuilder();
        var bytes = new char[1024];
        while (true)
        {
            var count = await reader.ReadAsync(bytes, token).ConfigureAwait(false);
            if (count == 0)
            {
                return result.ToString();
            }

            if (result.Length + count > limit)
            {
                throw new InvalidDataException("Downloader diagnostic output exceeded its safety limit.");
            }

            result.Append(bytes, 0, count);
        }
    }

    internal static string Classify(string error)
    {
        if (error.Contains("CERTIFICATE_VERIFY_FAILED", StringComparison.OrdinalIgnoreCase))
        {
            return "certificateRejected";
        }

        if (error.Contains("HTTP Error 401", StringComparison.OrdinalIgnoreCase) || error.Contains("HTTP Error 403", StringComparison.OrdinalIgnoreCase))
        {
            return "sourceDenied";
        }

        if (error.Contains("max-filesize", StringComparison.OrdinalIgnoreCase) || error.Contains("larger than max", StringComparison.OrdinalIgnoreCase))
        {
            return "sizeLimit";
        }

        if (error.Contains("HTTP Error 429", StringComparison.OrdinalIgnoreCase))
        {
            return "sourceRateLimited";
        }

        if (error.Contains("Unsupported URL", StringComparison.OrdinalIgnoreCase))
        {
            return "unsupportedSource";
        }

        foreach (var marker in new[] { "HTTP Error 502", "HTTP Error 503", "HTTP Error 504", "Tunnel connection failed: 502", "Connection refused", "Network is unreachable", "Name or service not known", "Temporary failure in name resolution", "timed out" })
        {
            if (error.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return "sourceNetwork";
            }
        }

        return "downloadFailed";
    }

    internal sealed record Result(int ExitCode, string ErrorCode)
    {
        internal string Output { get; init; } = string.Empty;
    }
}
