using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;

namespace Jellyfin.Api.Castlumora;

/// <summary>Platform-specific managed asset and filesystem behavior; no URL/process flags from clients.</summary>
internal sealed record YtDlpPlatform(string Id, string Version, string FileName, string Sha256, long Size)
{
    internal static JsonObject Release()
    {
        using var stream = typeof(YtDlpPlatform).Assembly.GetManifestResourceStream("Castlumora.YtDlpRelease")
            ?? throw new InvalidOperationException("Managed yt-dlp release manifest is unavailable.");
        return JsonNode.Parse(stream)!.AsObject();
    }

    internal static string CurrentId()
    {
        var os = OperatingSystem.IsLinux() ? "linux" : OperatingSystem.IsWindows() ? "win" : "unsupported";
        return os + "-" + RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
    }

    internal static YtDlpPlatform? Detect(string id)
    {
        var release = Release();
        var asset = release["assets"]?[id];
        return asset is null ? null : new YtDlpPlatform(id, release["version"]!.GetValue<string>(), asset["fileName"]!.GetValue<string>(), asset["sha256"]!.GetValue<string>(), asset["size"]!.GetValue<long>());
    }

    internal static void RejectLink(string path)
    {
        if (File.Exists(path) || Directory.Exists(path))
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("Managed tool paths must not be symbolic links or reparse points.");
            }
        }
        else if (new FileInfo(path).LinkTarget is not null)
        {
            throw new InvalidDataException("Managed tool path is a dangling symbolic link.");
        }
    }

    internal static void PrivateDirectory(string path)
    {
        RejectLink(path);
        Directory.CreateDirectory(path);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    internal static void Executable(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }
}
