using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Enums;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.MediaSegments;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model;
using MediaBrowser.Model.MediaSegments;

namespace Jellyfin.Api.Castlumora;

/// <summary>Built-in, opt-in local repeated-audio analysis and centrally reviewed intervals.</summary>
public sealed class SkipperService : IMediaSegmentProvider, IDisposable
{
    private readonly string _directory;
    private readonly ILibraryManager _library;
    private readonly IMediaEncoder _encoder;
    private readonly Lazy<ISessionManager> _sessions;
    private readonly SemaphoreSlim _files = new(1, 1);
    private readonly SemaphoreSlim _analysis = new(1, 1);

    /// <summary>Initializes a new instance of the <see cref="SkipperService"/> class.</summary>
    /// <param name="paths">Server configuration paths.</param>
    /// <param name="library">Existing library.</param>
    /// <param name="encoder">Existing configured FFmpeg.</param>
    /// <param name="sessions">Existing playback sessions.</param>
    public SkipperService(IApplicationPaths paths, ILibraryManager library, IMediaEncoder encoder, Lazy<ISessionManager> sessions)
    {
        _directory = Path.Combine(paths.ConfigurationDirectoryPath, "castlumora", "skipper");
        _library = library;
        _encoder = encoder;
        _sessions = sessions;
    }

    /// <inheritdoc />
    public string Name => "Castlumora Built-in";

    /// <summary>Gets settings or profile preferences.</summary>
    /// <param name="userId">Null for global settings.</param>
    /// <returns>Stored configuration.</returns>
    public Task<JsonObject> SettingsAsync(Guid? userId = null) => ReadAsync(userId.HasValue ? "profile-" + userId.Value.ToString("N") : "settings", userId.HasValue ? """{"mode":"default"}""" : """{"analysisEnabled":false,"defaultMode":"manual"}""");

    /// <summary>Saves validated settings or profile preferences.</summary>
    /// <param name="value">New configuration.</param>
    /// <param name="userId">Null for administrator settings.</param>
    /// <returns>Saved configuration.</returns>
    public async Task<JsonObject> SaveSettingsAsync(JsonObject value, Guid? userId = null)
    {
        var mode = value[userId.HasValue ? "mode" : "defaultMode"]?.GetValue<string>();
        if (mode is not ("manual" or "automatic" or "disabled") && !(userId.HasValue && mode == "default"))
        {
            throw new ArgumentException("Invalid skip mode.");
        }

        var settings = new JsonObject { [userId.HasValue ? "mode" : "defaultMode"] = mode };
        if (!userId.HasValue)
        {
            settings["analysisEnabled"] = value["analysisEnabled"]?.GetValue<bool>() ?? false;
        }

        await WriteAsync(userId.HasValue ? "profile-" + userId.Value.ToString("N") : "settings", settings).ConfigureAwait(false);
        return settings;
    }

    /// <summary>Gets central suggestions and reviewed intervals for an item.</summary>
    /// <param name="id">Item ID.</param>
    /// <returns>Revisioned state.</returns>
    public Task<JsonObject> ItemAsync(Guid id) => ReadAsync("item-" + id.ToString("N"), """{"revision":0,"segments":[]}""");

    /// <summary>Validates and replaces the reviewed interval list with a revision check.</summary>
    /// <param name="item">Existing local episode.</param>
    /// <param name="value">Revision and intervals.</param>
    /// <returns>Saved state.</returns>
    public async Task<JsonObject> SaveItemAsync(BaseItem item, JsonObject value)
    {
        var segments = value["segments"]?.AsArray() ?? throw new ArgumentException("Missing segments.");
        if (segments.Count > 10)
        {
            throw new ArgumentException("Too many intervals.");
        }

        var protectedFrom = value["protectedFromTicks"]?.GetValue<long>();
        if (protectedFrom.HasValue && (protectedFrom < 0 || protectedFrom > (item.RunTimeTicks ?? 0)))
        {
            throw new ArgumentException("Invalid protected post-credit boundary.");
        }

        foreach (var segment in segments)
        {
            var start = segment!["startTicks"]!.GetValue<long>();
            var end = segment["endTicks"]!.GetValue<long>();
            if (segment["type"]?.GetValue<string>() is not ("Intro" or "Outro") || segment["status"]?.GetValue<string>() is not ("approved" or "suggested") || start < 0 || end <= start || end > (item.RunTimeTicks ?? 0) || (segment["type"]!.GetValue<string>() == "Outro" && protectedFrom.HasValue && end > protectedFrom.Value))
            {
                throw new ArgumentException("Invalid interval bounds, type or review status.");
            }
        }

        await _files.WaitAsync().ConfigureAwait(false);
        try
        {
            var name = "item-" + item.Id.ToString("N");
            var current = await ReadFileAsync(name, """{"revision":0,"segments":[]}""").ConfigureAwait(false);
            if (current["revision"]!.GetValue<int>() != value["revision"]?.GetValue<int>())
            {
                throw new InvalidOperationException("Intervals changed. Reload before saving.");
            }

            var saved = new JsonObject { ["revision"] = current["revision"]!.GetValue<int>() + 1, ["sourceSignature"] = Signature(item), ["protectedFromTicks"] = protectedFrom, ["segments"] = segments.DeepClone() };
            await WriteFileAsync(name, saved).ConfigureAwait(false);
            return saved;
        }
        finally
        {
            _files.Release();
        }
    }

    /// <inheritdoc />
    public ValueTask<bool> Supports(BaseItem item) => ValueTask.FromResult(item is Episode && Path.IsPathFullyQualified(item.Path) && File.Exists(item.Path));

    /// <inheritdoc />
    public async Task<IReadOnlyList<MediaSegmentDto>> GetMediaSegments(MediaSegmentGenerationRequest request, CancellationToken cancellationToken)
    {
        var item = _library.GetItemById(request.ItemId);
        if (item is null || !await Supports(item).ConfigureAwait(false))
        {
            return [];
        }

        var state = await ItemAsync(item.Id).ConfigureAwait(false);
        var settings = await SettingsAsync().ConfigureAwait(false);
        if (settings["analysisEnabled"]!.GetValue<bool>() && state["sourceSignature"]?.GetValue<string>() != Signature(item) && !_sessions.Value.Sessions.Any(session => session.NowPlayingItem is not null))
        {
            state = await AnalyzeAsync(item, cancellationToken).ConfigureAwait(false);
        }

        if (state["sourceSignature"]?.GetValue<string>() != Signature(item))
        {
            return [];
        }

        return state["segments"]!.AsArray().Where(s => s!["status"]!.GetValue<string>() == "approved").Select(s => new MediaSegmentDto
        {
            ItemId = item.Id,
            Type = Enum.Parse<MediaSegmentType>(s!["type"]!.GetValue<string>()),
            StartTicks = s["startTicks"]!.GetValue<long>(),
            EndTicks = s["endTicks"]!.GetValue<long>()
        }).ToArray();
    }

    /// <summary>Analyzes bounded windows against at least two same-season peers; results require review.</summary>
    /// <param name="item">Target episode.</param>
    /// <param name="cancellationToken">Abort token.</param>
    /// <returns>Revisioned suggestions, never automatically approved.</returns>
    public async Task<JsonObject> AnalyzeAsync(BaseItem item, CancellationToken cancellationToken)
    {
        if (!await Supports(item).ConfigureAwait(false) || item is not Episode episode || episode.SeasonId.Equals(Guid.Empty))
        {
            throw new ArgumentException("A local episode with a season and duration is required.");
        }

        if (!(await SettingsAsync().ConfigureAwait(false))["analysisEnabled"]!.GetValue<bool>())
        {
            throw new InvalidOperationException("Enable optional analysis first.");
        }

        await _analysis.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_sessions.Value.Sessions.Any(session => session.NowPlayingItem is not null))
            {
                throw new InvalidOperationException("Analysis is deferred while a playback session is active.");
            }

            var state = await ItemAsync(item.Id).ConfigureAwait(false);
            var sourceSignature = Signature(item);
            var peers = _library.GetItemList(new InternalItemsQuery
            {
                ParentId = episode.SeasonId,
                IncludeItemTypes = [BaseItemKind.Episode],
                IsVirtualItem = false,
                DtoOptions = DtoOptions.StoredColumnsOnly,
                Limit = 12
            }).Where(p => !p.Id.Equals(item.Id) && Path.IsPathFullyQualified(p.Path) && File.Exists(p.Path)).Take(3).ToArray();
            if (peers.Length < 2 || (item.RunTimeTicks ?? 0) < TimeSpan.FromMinutes(2).Ticks)
            {
                throw new ArgumentException("At least three local episodes and a two-minute target duration are required.");
            }

            var suggestions = new JsonArray();
            foreach (var outro in new[] { false, true })
            {
                var target = await FingerprintsAsync(item, outro, cancellationToken).ConfigureAwait(false);
                var others = new List<ulong?[]>();
                foreach (var peer in peers)
                {
                    // Streaming takes priority; at most one FFmpeg process and one decode thread are used.
                    if (_sessions.Value.Sessions.Any(session => session.NowPlayingItem is not null))
                    {
                        throw new InvalidOperationException("Analysis paused because playback started.");
                    }

                    others.Add((await FingerprintsAsync(peer, outro, cancellationToken).ConfigureAwait(false)).Prints);
                    await Task.Delay(250, cancellationToken).ConfigureAwait(false);
                }

                var interval = AudioFingerprint.FindRepeated(target.Prints, others);
                if (interval.HasValue && (!outro || state["protectedFromTicks"] is null || TimeSpan.FromSeconds(target.Offset + interval.Value.End).Ticks <= state["protectedFromTicks"]!.GetValue<long>()))
                {
                    suggestions.Add(new JsonObject
                    {
                        ["type"] = outro ? "Outro" : "Intro",
                        ["startTicks"] = TimeSpan.FromSeconds(target.Offset + interval.Value.Start).Ticks,
                        ["endTicks"] = TimeSpan.FromSeconds(target.Offset + interval.Value.End).Ticks,
                        ["status"] = "suggested"
                    });
                }
            }

            // Preserve explicit reviewed intervals on unchanged files; never turn a suggestion into a skip silently.
            if (state["sourceSignature"]?.GetValue<string>() == Signature(item))
            {
                foreach (var approved in state["segments"]!.AsArray().Where(s => s!["status"]!.GetValue<string>() == "approved"))
                {
                    var type = approved!["type"]!.GetValue<string>();
                    for (var i = suggestions.Count - 1; i >= 0; i--)
                    {
                        if (suggestions[i]!["type"]!.GetValue<string>() == type)
                        {
                            suggestions.RemoveAt(i);
                        }
                    }

                    suggestions.Add(approved.DeepClone());
                }
            }

            if (Signature(item) != sourceSignature)
            {
                throw new InvalidOperationException("Media changed during analysis; refresh and retry.");
            }

            return await SaveItemAsync(item, new JsonObject { ["revision"] = state["revision"]!.GetValue<int>(), ["segments"] = suggestions, ["protectedFromTicks"] = state["protectedFromTicks"]?.DeepClone() }).ConfigureAwait(false);
        }
        finally
        {
            _analysis.Release();
        }
    }

    private async Task<(ulong?[] Prints, double Offset)> FingerprintsAsync(BaseItem item, bool outro, CancellationToken cancellationToken)
    {
        var duration = TimeSpan.FromTicks(item.RunTimeTicks ?? 0).TotalSeconds;
        var length = Math.Min(outro ? 180 : 300, duration / 3);
        var offset = outro ? Math.Max(0, duration - length) : 0;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(1));
        using var process = new Process { StartInfo = new ProcessStartInfo(_encoder.EncoderPath) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true } };
        foreach (var argument in new[] { "-nostdin", "-hide_banner", "-v", "error", "-threads", "1", "-ss", offset.ToString(CultureInfo.InvariantCulture), "-i", item.Path, "-t", length.ToString(CultureInfo.InvariantCulture), "-vn", "-ac", "1", "-ar", "8000", "-f", "s16le", "pipe:1" })
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        try
        {
            var errors = process.StandardError.BaseStream.CopyToAsync(Stream.Null, timeout.Token);
            using var buffer = new MemoryStream();
            var chunk = new byte[16384];
            int count;
            while ((count = await process.StandardOutput.BaseStream.ReadAsync(chunk, timeout.Token).ConfigureAwait(false)) > 0)
            {
                if (_sessions.Value.Sessions.Any(session => session.NowPlayingItem is not null))
                {
                    throw new InvalidOperationException("Analysis paused because playback started.");
                }

                if (buffer.Length + count > 5000000)
                {
                    throw new InvalidOperationException("Analysis output exceeded its bounded window.");
                }

                await buffer.WriteAsync(chunk.AsMemory(0, count), timeout.Token).ConfigureAwait(false);
            }

            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            await errors.ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException("Local audio analysis failed; check the media and configured FFmpeg.");
            }

            return (AudioFingerprint.Compute(buffer.ToArray()), offset);
        }
        finally
        {
            if (!process.HasExited)
            {
                // This process object owns only the FFmpeg instance started immediately above.
                process.Kill();
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    public async Task CleanupExtractedData(Guid itemId, CancellationToken cancellationToken)
    {
        await _files.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            File.Delete(Path.Combine(_directory, "item-" + itemId.ToString("N") + ".json"));
        }
        finally
        {
            _files.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _analysis.Dispose();
        _files.Dispose();
    }

    private static string Signature(BaseItem item)
    {
        var file = new FileInfo(item.Path);
        return file.Length.ToString(CultureInfo.InvariantCulture) + ":" + file.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture);
    }

    private async Task<JsonObject> ReadAsync(string name, string fallback)
    {
        await _files.WaitAsync().ConfigureAwait(false);
        try
        {
            return await ReadFileAsync(name, fallback).ConfigureAwait(false);
        }
        finally
        {
            _files.Release();
        }
    }

    private async Task<JsonObject> ReadFileAsync(string name, string fallback)
    {
        var path = Path.Combine(_directory, name + ".json");
        return JsonNode.Parse(File.Exists(path) ? await File.ReadAllTextAsync(path).ConfigureAwait(false) : fallback)!.AsObject();
    }

    private async Task WriteAsync(string name, JsonObject value)
    {
        await _files.WaitAsync().ConfigureAwait(false);
        try
        {
            await WriteFileAsync(name, value).ConfigureAwait(false);
        }
        finally
        {
            _files.Release();
        }
    }

    private async Task WriteFileAsync(string name, JsonObject value)
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, name + ".json");
        var temporary = path + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, value.ToJsonString()).ConfigureAwait(false);
            File.Move(temporary, path, true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
