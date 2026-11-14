using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Api.Castlumora;

/// <summary>Conservative self-contained H264/AAC MP4 boundary before any future library write.</summary>
internal static class ImportMediaInspection
{
    internal static async Task<double> InspectAsync(string path, string probe, string encoder, CancellationToken token, long maximumBytes = 33554432, int maximumHeight = 4096)
    {
        CheckContainer(path, maximumBytes);
        var start = Start(path, probe);
        var result = await ImportProcess.RunAsync(start, string.Empty, TimeSpan.FromSeconds(30), 16384, stop => Task.Delay(Timeout.InfiniteTimeSpan, stop), token).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidDataException("The downloaded file did not pass bounded MP4 inspection.");
        }

        var seconds = Properties(result.Output, maximumHeight);
        var decode = Start(path, encoder);
        decode.ArgumentList.Clear();
        foreach (var value in new[]
        {
            "-hide_banner", "-v", "error", "-nostdin", "-max_alloc", "16777216", "-threads", "1", "-filter_threads", "1", "-xerror",
            "-protocol_whitelist", "file", "-format_whitelist", "mov", "-enable_drefs", "0", "-use_absolute_path", "0",
            "-i", path, "-map", "0:v:0", "-frames:v", "1", "-an", "-sn", "-dn", "-progress", "pipe:1", "-f", "null", "-"
        })
        {
            decode.ArgumentList.Add(value);
        }

        var decoded = await ImportProcess.RunAsync(decode, string.Empty, TimeSpan.FromSeconds(30), 16384, stop => Task.Delay(Timeout.InfiniteTimeSpan, stop), token).ConfigureAwait(false);
        if (decoded.ExitCode != 0 || !decoded.Output.Split('\n').Any(line => line.Trim() == "frame=1"))
        {
            throw new InvalidDataException("The imported video did not produce a valid frame within inspection limits.");
        }

        return seconds;
    }

    internal static double Properties(string output, int maximumHeight = 4096)
    {
        try
        {
            using var document = JsonDocument.Parse(output);
            var root = document.RootElement;
            if (!root.TryGetProperty("format", out var format) || !format.TryGetProperty("duration", out var duration)
                || !double.TryParse(duration.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
                || !double.IsFinite(seconds) || seconds is <= 0 or > 10800
                || !root.TryGetProperty("streams", out var streams) || streams.GetArrayLength() is < 1 or > 8)
            {
                throw new InvalidDataException("The downloaded MP4 has invalid or unsupported media properties.");
            }

            var video = false;
            foreach (var stream in streams.EnumerateArray())
            {
                var codec = stream.GetProperty("codec_name").GetString();
                var type = stream.GetProperty("codec_type").GetString();
                if (codec == "h264" && type == "video"
                    && stream.GetProperty("width").GetInt32() is > 0 and <= 4096
                    && stream.GetProperty("height").GetInt32() > 0 && stream.GetProperty("height").GetInt32() <= maximumHeight)
                {
                    video = true;
                }
                else if (codec != "aac" || type != "audio")
                {
                    throw new InvalidDataException("Only H264 video and optional AAC audio are supported in this import increment.");
                }
            }

            if (!video)
            {
                throw new InvalidDataException("The import must contain a supported video stream.");
            }

            return seconds;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        {
            throw new InvalidDataException("The media probe returned invalid or unsupported properties.");
        }
    }

    internal static ProcessStartInfo Start(string path, string probe)
    {
        if (!Path.IsPathFullyQualified(path) || !Path.IsPathFullyQualified(probe))
        {
            throw new InvalidDataException("Inspection requires server-owned absolute file and probe paths.");
        }

        var start = new ProcessStartInfo(probe) { WorkingDirectory = Path.GetDirectoryName(path)! };
        foreach (var value in new[]
        {
            "-hide_banner", "-v", "error", "-max_alloc", "16777216", "-probesize", "8388608", "-analyzeduration", "5000000",
            "-protocol_whitelist", "file", "-format_whitelist", "mov", "-enable_drefs", "0", "-use_absolute_path", "0",
            "-show_entries", "stream=codec_name,codec_type,width,height:format=duration", "-of", "json", "-i", path
        })
        {
            start.ArgumentList.Add(value);
        }

        start.Environment.Clear();
        start.Environment["HOME"] = start.WorkingDirectory;
        start.Environment["TMPDIR"] = start.WorkingDirectory;
        start.Environment["TMP"] = start.WorkingDirectory;
        start.Environment["TEMP"] = start.WorkingDirectory;
        if (OperatingSystem.IsWindows())
        {
            start.Environment["SystemRoot"] = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        }
        else
        {
            start.Environment["PATH"] = "/usr/bin:/bin";
            start.Environment["LANG"] = "C.UTF-8";
        }

        return start;
    }

    internal static void CheckContainer(string path, long maximumBytes = 33554432, bool requireVideo = true, bool allowFragments = false)
    {
        YtDlpPlatform.RejectLink(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (maximumBytes is <= 0 or > YouTubeImportOptions.AbsoluteMaximumBytes || stream.Length < 32 || stream.Length > maximumBytes)
        {
            throw new InvalidDataException("Import MP4 size is outside the supported limit.");
        }

        var inspection = new Boxes(stream, allowFragments);
        inspection.Walk(stream.Length, 0);
        if (inspection.Movies != 1 || (allowFragments ? inspection.Media < 1 : inspection.Media != 1) || inspection.FileTypes != 1 || (requireVideo && inspection.VideoEntries < 1)
            || inspection.References < 1 || inspection.SampleDescriptions != inspection.References)
        {
            throw new InvalidDataException("A complete self-contained non-fragmented MP4 is required.");
        }
    }

    private sealed class Boxes(FileStream stream, bool allowFragments)
    {
        private int _count;

        internal int Movies { get; private set; }

        internal int Media { get; private set; }

        internal int FileTypes { get; private set; }

        internal int References { get; private set; }

        internal int SampleDescriptions { get; private set; }

        internal int VideoEntries { get; private set; }

        internal void Walk(long end, int depth)
        {
            if (depth > 8)
            {
                throw new InvalidDataException("The MP4 structure is too deeply nested.");
            }

            Span<byte> entry = stackalloc byte[8];
            while (stream.Position < end)
            {
                var (type, stop) = Header(end);
                if (depth == 0 && type is not ("ftyp" or "moov" or "mdat" or "free" or "skip" or "wide")
                    && !(allowFragments && type is "moof" or "mfra" or "sidx" or "styp"))
                {
                    throw new InvalidDataException("Unsupported MP4 top-level structure.");
                }

                switch (type)
                {
                    case "ftyp":
                        FileTypes++;
                        if (depth != 0 || stream.Position != 8 || stop is < 16 or > 128)
                        {
                            throw new InvalidDataException("The MP4 file type must be first and bounded.");
                        }

                        break;
                    case "moov":
                        Movies++;
                        Walk(stop, depth + 1);
                        break;
                    case "mdat":
                        Media++;
                        if (stop == stream.Position)
                        {
                            throw new InvalidDataException("The MP4 media payload is empty.");
                        }

                        break;
                    case "trak":
                    case "mdia":
                    case "minf":
                    case "dinf":
                    case "stbl":
                    case "edts":
                        Walk(stop, depth + 1);
                        break;
                    case "dref":
                        References++;
                        if (U32(stop) != 0 || U32(stop) != 1)
                        {
                            throw new InvalidDataException("MP4 external or multiple data references are not supported.");
                        }

                        var (reference, referenceEnd) = Header(stop);
                        if (reference != "url " || U32(referenceEnd) != 1 || stream.Position != referenceEnd || referenceEnd != stop)
                        {
                            throw new InvalidDataException("MP4 references must be explicitly self-contained.");
                        }

                        break;
                    case "stsd":
                        SampleDescriptions++;
                        if (U32(stop) != 0 || U32(stop) != 1)
                        {
                            throw new InvalidDataException("MP4 sample descriptions are unsupported.");
                        }

                        var (sample, sampleEnd) = Header(stop);
                        if (sample is not ("avc1" or "mp4a") || sampleEnd != stop || sampleEnd - stream.Position < 8)
                        {
                            throw new InvalidDataException("Only unprotected H264/AAC MP4 samples are accepted.");
                        }

                        var body = stream.Position;
                        stream.ReadExactly(entry);
                        if (BinaryPrimitives.ReadUInt16BigEndian(entry[6..]) != 1)
                        {
                            throw new InvalidDataException("MP4 samples cannot select an external data reference.");
                        }

                        var headerBytes = sample == "avc1" ? 78 : 28;
                        if (sampleEnd - body < headerBytes)
                        {
                            throw new InvalidDataException("Truncated MP4 sample entry.");
                        }

                        if (sample == "mp4a" && U32(sampleEnd) != 0)
                        {
                            throw new InvalidDataException("Only standard MP4 audio sample entries are supported.");
                        }

                        stream.Position = body + headerBytes;
                        Walk(sampleEnd, depth + 1);
                        VideoEntries += sample == "avc1" ? 1 : 0;
                        break;
                    case "moof":
                    case "mvex":
                        if (allowFragments)
                        {
                            break;
                        }

                        throw new InvalidDataException("Fragmented inputs require the bounded local remux boundary.");
                    case "sinf":
                        throw new InvalidDataException("Fragmented or protected media is not supported by this increment.");
                }

                stream.Position = stop;
            }
        }

        private (string Type, long End) Header(long end)
        {
            var start = stream.Position;
            if (++_count > 4096 || end - start < 8)
            {
                throw new InvalidDataException("MP4 box count or bounds are invalid.");
            }

            Span<byte> header = stackalloc byte[8];
            stream.ReadExactly(header);
            var size = (long)BinaryPrimitives.ReadUInt32BigEndian(header);
            var type = Encoding.ASCII.GetString(header[4..]);
            if (size == 1)
            {
                if (end - stream.Position < 8)
                {
                    throw new InvalidDataException("Truncated MP4 extended size.");
                }

                stream.ReadExactly(header);
                var extended = BinaryPrimitives.ReadUInt64BigEndian(header);
                if (extended > (ulong)(end - start))
                {
                    throw new InvalidDataException("MP4 extended size exceeds its containing box.");
                }

                size = (long)extended;
            }
            else if (size == 0)
            {
                size = end - start;
            }

            if (size < stream.Position - start || size > end - start)
            {
                throw new InvalidDataException("MP4 box exceeds its containing bounds.");
            }

            return (type, start + size);
        }

        private uint U32(long end)
        {
            if (end - stream.Position < 4)
            {
                throw new InvalidDataException("Truncated MP4 field.");
            }

            Span<byte> bytes = stackalloc byte[4];
            stream.ReadExactly(bytes);
            return BinaryPrimitives.ReadUInt32BigEndian(bytes);
        }
    }
}
