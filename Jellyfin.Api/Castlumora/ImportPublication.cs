using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Jellyfin.Data;
using Jellyfin.Database.Implementations.Enums;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Api.Castlumora;

/// <summary>
/// Internal idempotent publication boundary used by the hosted import service.
/// A resolver must reauthorize the requesting administrator and real destination on each call.
/// </summary>
internal sealed class ImportPublication
{
    private readonly string _data;
    private readonly string[] _protected;
    private readonly Func<ImportQueue.Job, string> _destination;
    private readonly Func<ImportQueue.Job, Action<long>, CancellationToken, Task<YtDlpImportRunner.StagedDownload>> _download;
    private readonly Action<string> _scan;
    private readonly Func<(string Probe, string Encoder)> _mediaTools;
    private readonly ILogger _logger;

    internal ImportPublication(YtDlpRuntime runtime, IApplicationPaths paths, ILibraryManager libraries, IUserManager users, ILibraryMonitor monitor, IMediaEncoder encoder, ILogger logger)
        : this(
            paths.DataPath,
            [paths.ProgramDataPath, paths.ConfigurationDirectoryPath, paths.ProgramSystemPath, paths.WebPath, paths.CachePath],
            job => Resolve(job, libraries, users),
            (job, progress, token) => job.YouTube is null
                ? new YtDlpImportRunner(runtime, paths, logger).RunAsync(job.Source, token, progress)
                : new YouTubeDownload(runtime, paths, encoder.EncoderPath, logger).RunAsync(job, progress, token),
            path => RequestScan(path, libraries, monitor),
            encoder.ProbePath,
            encoder.EncoderPath,
            logger,
            () => (encoder.ProbePath, encoder.EncoderPath))
    {
    }

    internal ImportPublication(
        string data,
        string[] protectedPaths,
        Func<ImportQueue.Job, string> destination,
        Func<ImportQueue.Job, Action<long>, CancellationToken, Task<YtDlpImportRunner.StagedDownload>> download,
        Action<string> scan,
        string probe,
        string encoder,
        ILogger logger,
        Func<(string Probe, string Encoder)>? mediaTools = null)
    {
        _data = Normalize(data);
        CheckPath(_data);
        _protected = protectedPaths.Append(data).Where(path => !string.IsNullOrWhiteSpace(path)).Select(Normalize).ToArray();
        _destination = destination;
        _download = download;
        _scan = scan;
        _mediaTools = mediaTools ?? (() => (probe, encoder));
        _logger = logger;
    }

    internal void ValidateDestination(Guid administrator, Guid library)
    {
        Destination(new ImportQueue.Job(Guid.Empty, string.Empty, string.Empty, library, string.Empty, string.Empty, administrator, DateTimeOffset.UtcNow));
    }

    internal async Task<ImportQueue.Receipt> ExecuteAsync(ImportQueue.Job job, Action<long> progress, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var root = Destination(job);
        var folder = Path.Combine(root, "Castlumora Imports");
        PrepareFolder(folder, job.DestinationId);
        var ownership = Path.Combine(folder, ".publication.lock");
        YtDlpPlatform.RejectLink(ownership);
        using var reservation = new FileStream(ownership, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        PrivateFile(ownership);
        var recordPath = RecordPath(job.Id);
        var prepared = Read(recordPath, job, root);
        if (prepared is not null)
        {
            var existing = Media(folder, prepared.Sha256);
            if (File.Exists(existing))
            {
                await VerifyAsync(existing, prepared.Sha256, prepared.Bytes, token, job.YouTube?.MaximumBytes ?? 33554432).ConfigureAwait(false);
                CheckDestination(job, root);
                token.ThrowIfCancellationRequested();
                _scan(existing);
                return new ImportQueue.Receipt(prepared.Sha256, prepared.Bytes);
            }
        }

        using var staged = await _download(job, progress, token).ConfigureAwait(false);
        var tools = _mediaTools();
        await ImportMediaInspection.InspectAsync(staged.MediaPath, tools.Probe, tools.Encoder, token, job.YouTube?.MaximumBytes ?? 33554432, job.YouTube?.Height ?? 4096).ConfigureAwait(false);
        await VerifyAsync(staged.MediaPath, staged.Sha256, staged.Bytes, token, job.YouTube?.MaximumBytes ?? 33554432).ConfigureAwait(false);
        if (prepared is not null && (prepared.Sha256 != staged.Sha256 || prepared.Bytes != staged.Bytes))
        {
            throw new InvalidDataException("The source changed after an interrupted publication; retain the record and submit a reviewed new import.");
        }

        CheckDestination(job, root);
        var record = prepared ?? new Prepared(1, job, root, staged.Sha256, staged.Bytes);
        if (prepared is null)
        {
            Write(recordPath, record);
        }

        var target = Media(folder, record.Sha256);
        var nfo = Path.ChangeExtension(target, ".nfo");
        var partial = Path.Combine(folder, ".castlumora-" + job.Id.ToString("N") + ".part");
        var metadataPartial = partial + ".metadata";
        var metadata = Metadata(record);
        var committed = File.Exists(target);
        var ownedNfo = false;
        try
        {
            if (!committed)
            {
                CheckPath(folder);
                YtDlpPlatform.RejectLink(partial);
                // This name is reserved to a persisted job ID under a validated, non-shared-writable root.
                File.Delete(partial);
                using (var input = new FileStream(staged.MediaPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan))
                await using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    PrivateFile(partial);
                    await input.CopyToAsync(output, token).ConfigureAwait(false);
                    await output.FlushAsync(token).ConfigureAwait(false);
                    RandomAccess.FlushToDisk(output.SafeFileHandle);
                }

                await VerifyAsync(partial, record.Sha256, record.Bytes, token, job.YouTube?.MaximumBytes ?? 33554432).ConfigureAwait(false);
                CheckDestination(job, root);
                YtDlpPlatform.RejectLink(nfo);
                if (File.Exists(nfo))
                {
                    if (new FileInfo(nfo).Length > 32768 || !(await File.ReadAllBytesAsync(nfo, token).ConfigureAwait(false)).AsSpan().SequenceEqual(metadata))
                    {
                        throw new InvalidDataException("Destination metadata already exists; it was retained for administrator review.");
                    }
                }
                else
                {
                    YtDlpPlatform.RejectLink(metadataPartial);
                    File.Delete(metadataPartial);
                    await using (var output = new FileStream(metadataPartial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
                    {
                        PrivateFile(metadataPartial);
                        await output.WriteAsync(metadata, token).ConfigureAwait(false);
                        await output.FlushAsync(token).ConfigureAwait(false);
                        RandomAccess.FlushToDisk(output.SafeFileHandle);
                    }

                    File.Move(metadataPartial, nfo, overwrite: false);
                }

                ownedNfo = true;
                CheckDestination(job, root);
                token.ThrowIfCancellationRequested();
                // Same-directory rename is the publication commit point. No overwrite or cross-volume move.
                YtDlpPlatform.RejectLink(target);
                File.Move(partial, target, overwrite: false);
                committed = true;
            }

            await VerifyAsync(target, record.Sha256, record.Bytes, CancellationToken.None, job.YouTube?.MaximumBytes ?? 33554432).ConfigureAwait(false);
            _scan(target);
            // Cancellation after the commit point never reports that committed media was removed.
            return new ImportQueue.Receipt(record.Sha256, record.Bytes);
        }
        finally
        {
            Cleanup(partial, job.Id);
            Cleanup(metadataPartial, job.Id);
            if (!committed && ownedNfo)
            {
                try
                {
                    YtDlpPlatform.RejectLink(nfo);
                    if (File.Exists(nfo) && new FileInfo(nfo).Length <= 32768 && (await File.ReadAllBytesAsync(nfo, CancellationToken.None).ConfigureAwait(false)).AsSpan().SequenceEqual(metadata))
                    {
                        File.Delete(nfo);
                    }
                }
                catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
                {
                    _logger.LogWarning("Import {ImportId} owned metadata cleanup requires review", job.Id);
                }
            }
        }
    }

    internal static void RequestScan(string path, ILibraryManager libraries, ILibraryMonitor monitor)
    {
        monitor.ReportFileSystemChanged(path);
        var folder = Path.GetDirectoryName(path)!;
        monitor.ReportFileSystemChanged(folder);
        if (libraries.FindByPath(folder, true) is null)
        {
            // A new library can still be waiting for its first scan. A monitor hint
            // alone has no indexed ancestor in that case; queue, never cancel, a scan.
            libraries.QueueLibraryScan();
        }
    }

    internal static string Resolve(ImportQueue.Job job, ILibraryManager libraries, IUserManager users)
    {
        var user = users.GetUserById(job.RequestedBy);
        if (user is null || !user.HasPermission(PermissionKind.IsAdministrator) || user.HasPermission(PermissionKind.IsDisabled))
        {
            throw new UnauthorizedAccessException("An enabled administrator must authorize the import.");
        }

        var library = libraries.GetVirtualFolders().Find(folder => Guid.TryParse(folder.ItemId, out var id) && id.Equals(job.DestinationId));
        if (library?.CollectionType != CollectionTypeOptions.homevideos || library.Locations.Length != 1)
        {
            throw new InvalidDataException("Select an existing generic-video library with exactly one local destination.");
        }

        return library.Locations[0];
    }

    private string Destination(ImportQueue.Job job)
    {
        var root = Normalize(_destination(job));
        CheckPath(root);
        if (!Directory.Exists(root) || root.Equals(Path.GetPathRoot(root), StringComparison.OrdinalIgnoreCase)
            || _protected.Any(path => Contains(path, root) || Contains(root, path)))
        {
            throw new InvalidDataException("Import destinations must be existing media directories outside server state, program, web and cache paths.");
        }

        if (!OperatingSystem.IsWindows() && (File.GetUnixFileMode(root) & (UnixFileMode.GroupWrite | UnixFileMode.OtherWrite)) != 0)
        {
            throw new InvalidDataException("Use a destination that other operating-system accounts cannot modify.");
        }

        if (!OperatingSystem.IsWindows())
        {
            for (var parent = Path.GetDirectoryName(root); parent is not null; parent = Path.GetDirectoryName(parent))
            {
                var mode = File.GetUnixFileMode(parent);
                if ((mode & (UnixFileMode.GroupWrite | UnixFileMode.OtherWrite)) != 0 && (mode & UnixFileMode.StickyBit) == 0)
                {
                    throw new InvalidDataException("Destination ancestors must prevent other operating-system accounts from replacing the library directory.");
                }
            }
        }

        return root;
    }

    private void CheckDestination(ImportQueue.Job job, string expected)
    {
        if (!string.Equals(Destination(job), expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The destination changed during the import; no media was promoted.");
        }
    }

    private string RecordPath(Guid id)
    {
        var folder = _data;
        foreach (var segment in new[] { "castlumora", "imports", "publications" })
        {
            folder = Path.Combine(folder, segment);
            YtDlpPlatform.PrivateDirectory(folder);
        }

        return Path.Combine(folder, id.ToString("N") + ".json");
    }

    private static Prepared? Read(string path, ImportQueue.Job job, string root)
    {
        YtDlpPlatform.RejectLink(path);
        if (!File.Exists(path))
        {
            return null;
        }

        if (new FileInfo(path).Length > 65536)
        {
            throw new InvalidDataException("Invalid private publication record; preserve it for recovery.");
        }

        var record = JsonSerializer.Deserialize<Prepared>(File.ReadAllText(path));
        if (record is null || record.Version != 1 || record.Job is null || !record.Job.Id.Equals(job.Id)
            || !record.Job.DestinationId.Equals(job.DestinationId) || record.Job.SourceKey != job.SourceKey || record.Job.Source != job.Source
            || !record.Job.RequestedBy.Equals(job.RequestedBy) || record.Job.Rights != job.Rights || record.Job.YouTube != job.YouTube
            || record.Root != root || (record.Bytes <= 0 || record.Bytes > (job.YouTube?.MaximumBytes ?? 33554432)) || !Hash(record.Sha256))
        {
            throw new InvalidDataException("Publication identity, destination or content record is invalid; preserve it for recovery.");
        }

        return record;
    }

    private static void PrepareFolder(string path, Guid library)
    {
        CheckPath(path);
        var existed = Directory.Exists(path);
        var marker = Path.Combine(path, ".castlumora-library");
        YtDlpPlatform.RejectLink(marker);
        if (existed && !File.Exists(marker) && Directory.EnumerateFileSystemEntries(path).Any())
        {
            throw new InvalidDataException("The reserved import directory already contains unowned files; they were retained.");
        }

        if (!existed)
        {
            YtDlpPlatform.PrivateDirectory(path);
        }

        if (!OperatingSystem.IsWindows() && (File.GetUnixFileMode(path) & (UnixFileMode.GroupWrite | UnixFileMode.OtherWrite)) != 0)
        {
            throw new InvalidDataException("The reserved import directory must not be writable by other operating-system accounts.");
        }

        if (File.Exists(marker))
        {
            if (new FileInfo(marker).Length != 32 || File.ReadAllText(marker) != library.ToString("N"))
            {
                throw new InvalidDataException("The destination belongs to a different library; its contents were retained.");
            }
        }
        else
        {
            using var file = new FileStream(marker, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
            PrivateFile(marker);
            file.Write(Encoding.ASCII.GetBytes(library.ToString("N")));
            file.Flush(flushToDisk: true);
        }
    }

    private static string Normalize(string path)
    {
        if (!Path.IsPathFullyQualified(path) || path.StartsWith("//", StringComparison.Ordinal) || path.StartsWith("\\\\", StringComparison.Ordinal))
        {
            throw new InvalidDataException("A configured local absolute destination is required.");
        }

        path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        return OperatingSystem.IsMacOS()
            ? ImportQueue.MacDataDirectory(path, alias => new DirectoryInfo(alias).ResolveLinkTarget(returnFinalTarget: true)?.FullName)
            : path;
    }

    private static bool Contains(string parent, string child) => string.Equals(parent.Normalize(), child.Normalize(), StringComparison.OrdinalIgnoreCase)
        || child.Normalize().StartsWith(Path.EndsInDirectorySeparator(parent) ? parent.Normalize() : parent.Normalize() + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static void CheckPath(string path)
    {
        for (var current = path; current is not null; current = Path.GetDirectoryName(current))
        {
            YtDlpPlatform.RejectLink(current);
        }
    }

    private static bool Hash(string value) => value is { Length: 64 } && value.All(c => char.IsAsciiHexDigit(c) && !char.IsAsciiLetterUpper(c));

    private static string Media(string folder, string hash)
    {
        if (!Hash(hash))
        {
            throw new InvalidDataException("Invalid publication content identity.");
        }

        return Path.Combine(folder, hash + ".mp4");
    }

    private static byte[] Metadata(Prepared prepared)
    {
        var document = new XElement(
            "movie",
            new XElement("title", SafeTitle(prepared.Job.Title)),
            new XElement("lockdata", "true"),
            new XElement("uniqueid", new XAttribute("type", "CastlumoraImport"), new XAttribute("default", "false"), prepared.Job.Id.ToString("N")));
        if (prepared.Job.YouTube is { } creator)
        {
            document.Add(new XElement("uniqueid", new XAttribute("type", "YouTube"), creator.VideoId));
            document.Add(new XElement("uniqueid", new XAttribute("type", "YouTubeChannel"), creator.ChannelId));
            document.Add(new XElement("originaltitle", SafeTitle(creator.OriginalTitle)));
            document.Add(new XElement("studio", SafeTitle(creator.Creator)));
            document.Add(new XElement("plot", SafeTitle(creator.Description)));
            document.Add(new XElement("tag", "YouTube"));
        }

        return Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting));
    }

    private static string SafeTitle(string title)
    {
        var safe = string.Concat(title.EnumerateRunes().Where(rune => rune.Value > char.MaxValue || XmlConvert.IsXmlChar((char)rune.Value)).Select(rune => rune.ToString()));
        return string.IsNullOrWhiteSpace(safe) ? "Imported video" : safe;
    }

    private static async Task VerifyAsync(string path, string hash, long bytes, CancellationToken token, long maximumBytes = 33554432)
    {
        YtDlpPlatform.RejectLink(path);
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (file.Length != bytes || (bytes <= 0 || bytes > maximumBytes)
            || Convert.ToHexStringLower(await SHA256.HashDataAsync(file, token).ConfigureAwait(false)) != hash)
        {
            throw new InvalidDataException("Publication bytes changed or collide with existing media; existing files were retained.");
        }
    }

    private static void Write(string path, Prepared record)
    {
        CheckPath(Path.GetDirectoryName(path)!);
        YtDlpPlatform.RejectLink(path);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                PrivateFile(temporary);
                JsonSerializer.Serialize(file, record);
                if (file.Length > 65536)
                {
                    throw new InvalidDataException("Publication provenance exceeds the supported bound.");
                }

                file.Flush(flushToDisk: true);
            }

            File.Move(temporary, path, overwrite: false);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private static void PrivateFile(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private void Cleanup(string path, Guid job)
    {
        try
        {
            YtDlpPlatform.RejectLink(path);
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _logger.LogWarning("Import {ImportId} owned publication temporary cleanup requires review", job);
        }
    }

    private sealed record Prepared(int Version, ImportQueue.Job Job, string Root, string Sha256, long Bytes);
}
