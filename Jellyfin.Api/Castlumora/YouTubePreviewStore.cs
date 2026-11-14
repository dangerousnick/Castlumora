using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Jellyfin.Api.Castlumora;

/// <summary>Short-lived private confirmation receipts bound to an administrator and real library.</summary>
internal sealed class YouTubePreviewStore(string data)
{
    internal sealed record Preview(Guid Id, Guid Administrator, Guid Library, string Source, string Title, YouTubeImportOptions Options, DateTimeOffset Expires);

    internal Preview Create(Guid administrator, Guid library, string source, string title, YouTubeImportOptions options)
    {
        options.Validate(source);
        if (administrator == Guid.Empty || library == Guid.Empty)
        {
            throw new InvalidDataException("Authenticated administrator and library required.");
        }

        var root = Root();
        foreach (var path in Directory.EnumerateFiles(root, "*.json"))
        {
            YtDlpPlatform.RejectLink(path);
            if (Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "N", out _) && new FileInfo(path).Length <= 65536)
            {
                var existing = JsonSerializer.Deserialize<Preview>(File.ReadAllText(path));
                if (existing is not null && existing.Expires < DateTimeOffset.UtcNow)
                {
                    File.Delete(path);
                }
            }
        }

        if (Directory.EnumerateFiles(root).Count() >= 100)
        {
            throw new InvalidOperationException("The preview confirmation limit is reached; wait for receipts to expire.");
        }

        var receipt = new Preview(Guid.NewGuid(), administrator, library, source, title, options, DateTimeOffset.UtcNow.AddMinutes(20));
        var destination = Path.Combine(root, receipt.Id.ToString("N") + ".json");
        using var file = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(destination, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        JsonSerializer.Serialize(file, receipt);
        file.Flush(flushToDisk: true);
        return receipt;
    }

    internal Preview Read(Guid id, Guid administrator, Guid library)
    {
        var path = Path.Combine(Root(), id.ToString("N") + ".json");
        YtDlpPlatform.RejectLink(path);
        if (!File.Exists(path) || new FileInfo(path).Length > 65536)
        {
            throw new InvalidDataException("Preview confirmation is missing; preview the source again.");
        }

        var preview = JsonSerializer.Deserialize<Preview>(File.ReadAllText(path));
        if (preview is null || preview.Id != id || preview.Administrator != administrator || preview.Library != library
            || preview.Expires < DateTimeOffset.UtcNow || preview.Expires > DateTimeOffset.UtcNow.AddMinutes(21))
        {
            throw new InvalidDataException("Preview confirmation expired or belongs to another administrator/library.");
        }

        preview.Options.Validate(preview.Source);
        return preview;
    }

    private string Root()
    {
        var root = Path.Combine(data, "castlumora", "imports", "confirmations");
        for (var current = root; current is not null; current = Path.GetDirectoryName(current))
        {
            YtDlpPlatform.RejectLink(current);
        }

        YtDlpPlatform.PrivateDirectory(root);
        return root;
    }
}
