using System;
using System.Collections.Generic;
using System.IO;

namespace Jellyfin.Api.Castlumora;

/// <summary>Fixed public failure vocabulary; exception text and downloader output stay private.</summary>
internal static class ImportFailure
{
    private static readonly Dictionary<string, string> _messages = new(StringComparer.Ordinal)
    {
        ["certificateRejected"] = "Source HTTPS certificate validation failed.",
        ["sizeLimit"] = "The source exceeds the import size limit.",
        ["sourceDenied"] = "The source denied access; verify source permissions.",
        ["sourceNetwork"] = "The source network connection failed; verify server egress and source availability.",
        ["sourceRateLimited"] = "The source rate limited the import; wait before retrying.",
        ["unsupportedSource"] = "This source is not supported by the direct-video import preview.",
        ["incompleteOutput"] = "No complete media and metadata pair was produced; source format or size policy may reject this source.",
        ["unexpectedOutput"] = "Unexpected downloader output was rejected.",
        ["downloadFailed"] = "The source download failed; verify its availability and supported format."
    };

    internal static InvalidDataException Download(string code) => new(_messages.TryGetValue(code, out var message) ? message : _messages["downloadFailed"]);

    internal static string Code(Exception exception)
    {
        if (exception is InvalidDataException)
        {
            foreach (var pair in _messages)
            {
                if (string.Equals(exception.Message, pair.Value, StringComparison.Ordinal))
                {
                    return pair.Key;
                }
            }
        }

        return exception switch
        {
            TimeoutException => "timeLimit",
            UnauthorizedAccessException => "permissionDenied",
            _ => "executionFailed"
        };
    }

    internal static string? PublicCode(string? code) => code is null ? null
        : _messages.ContainsKey(code) || code is "timeLimit" or "permissionDenied" or "interrupted" ? code : "executionFailed";
}
