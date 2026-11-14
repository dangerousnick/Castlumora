using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Api.Castlumora;

/// <summary>Reviewed Linux JavaScript dependency; no PATH discovery or runtime auto-updates.</summary>
internal static class YouTubeNodeRuntime
{
    private static readonly JsonObject _release = Release();

    internal static string Version => _release["version"]!.GetValue<string>();

    internal static long Size => _release["binarySize"]!.GetValue<long>();

    internal static string Sha256 => _release["binarySha256"]!.GetValue<string>();

    private static JsonObject Release()
    {
        using var stream = typeof(YouTubeNodeRuntime).Assembly.GetManifestResourceStream("Castlumora.NodeRelease")
            ?? throw new InvalidOperationException("Reviewed Node release manifest is unavailable.");
        return JsonNode.Parse(stream)!.AsObject();
    }

    internal static async Task<string> VerifyAsync(string systemRoot, CancellationToken token)
    {
        if (!OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            throw new NotSupportedException("The reviewed YouTube JavaScript runtime currently targets Linux x64.");
        }

        var root = Path.GetFullPath(systemRoot);
        var executable = Path.Combine(root, "tools", "node", Version, "linux-x64", "node");
        for (var current = executable; current is not null; current = Path.GetDirectoryName(current))
        {
            YtDlpPlatform.RejectLink(current);
        }

        var info = new FileInfo(executable);
        if (!info.Exists || info.Length != Size)
        {
            throw new InvalidDataException("The reviewed YouTube JavaScript dependency is missing or has an unexpected size.");
        }

        var mode = File.GetUnixFileMode(executable);
        if ((mode & UnixFileMode.UserExecute) == 0 || (mode & (UnixFileMode.GroupWrite | UnixFileMode.OtherWrite)) != 0)
        {
            throw new InvalidDataException("The reviewed JavaScript dependency requires safe executable permissions.");
        }

        await using var file = new FileStream(executable, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(file, token).ConfigureAwait(false));
        if (hash != Sha256)
        {
            throw new InvalidDataException("The JavaScript dependency does not match its reviewed checksum.");
        }

        var start = new ProcessStartInfo(executable);
        start.ArgumentList.Add("--version");
        start.Environment.Clear();
        start.Environment["PATH"] = "/usr/bin:/bin";
        var result = await ImportProcess.RunAsync(start, string.Empty, TimeSpan.FromSeconds(10), 256, ct => Task.Delay(Timeout.InfiniteTimeSpan, ct), token).ConfigureAwait(false);
        if (result.ExitCode != 0 || result.Output.Trim() != "v" + Version)
        {
            throw new InvalidDataException("The reviewed JavaScript runtime failed its version check.");
        }

        return executable;
    }
}
