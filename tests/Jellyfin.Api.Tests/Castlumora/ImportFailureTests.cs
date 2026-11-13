using System;
using System.IO;
using Jellyfin.Api.Castlumora;
using Xunit;

namespace Jellyfin.Api.Tests.Castlumora;

public sealed class ImportFailureTests
{
    [Theory]
    [InlineData("certificateRejected")]
    [InlineData("sourceDenied")]
    [InlineData("sizeLimit")]
    [InlineData("sourceNetwork")]
    [InlineData("sourceRateLimited")]
    [InlineData("unsupportedSource")]
    [InlineData("downloadFailed")]
    [InlineData("incompleteOutput")]
    [InlineData("unexpectedOutput")]
    public void KnownDownloadFailures_ExposeOnlyFixedVocabulary(string code)
    {
        var error = ImportFailure.Download(code);
        Assert.Equal(code, ImportFailure.Code(error));
        Assert.Equal(code, ImportFailure.PublicCode(code));
        Assert.DoesNotContain("https://", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RawExceptionTextAndUnknownHistoryCodes_AreNeverExposed()
    {
        const string Secret = "https://private.invalid/file.mp4?token=do-not-publish";
        Assert.Equal("executionFailed", ImportFailure.Code(new IOException(Secret)));
        Assert.Equal("executionFailed", ImportFailure.Code(new InvalidDataException(Secret)));
        Assert.Equal("executionFailed", ImportFailure.PublicCode(Secret));
        Assert.DoesNotContain(Secret, ImportFailure.Download(Secret).ToString(), StringComparison.Ordinal);
        Assert.Equal("downloadFailed", ImportFailure.Code(ImportFailure.Download(Secret)));
        Assert.Null(ImportFailure.PublicCode(null));
        Assert.Equal("timeLimit", ImportFailure.Code(new TimeoutException(Secret)));
        Assert.Equal("permissionDenied", ImportFailure.Code(new UnauthorizedAccessException(Secret)));
    }

    [Theory]
    [InlineData("HTTP Error 403: denied https://private.invalid?token=secret", "sourceDenied")]
    [InlineData("CERTIFICATE_VERIFY_FAILED: token=secret", "certificateRejected")]
    [InlineData("HTTP Error 429: token=secret", "sourceRateLimited")]
    [InlineData("HTTP Error 502: source rejected https://private.invalid?token=secret", "sourceNetwork")]
    [InlineData("Tunnel connection failed: 502 Bad Gateway; token=secret", "sourceNetwork")]
    [InlineData("Connection refused: token=secret", "sourceNetwork")]
    [InlineData("Network is unreachable: token=secret", "sourceNetwork")]
    [InlineData("Unsupported URL: https://private.invalid?token=secret", "unsupportedSource")]
    [InlineData("larger than max-filesize; token=secret", "sizeLimit")]
    [InlineData("unknown output with token=secret", "downloadFailed")]
    public void BoundedProcessOutputClassification_ReturnsCodesNotOutput(string output, string expected)
    {
        var code = ImportProcess.Classify(output);
        Assert.Equal(expected, code);
        Assert.Equal(expected, ImportFailure.Code(ImportFailure.Download(code)));
        Assert.DoesNotContain("secret", ImportFailure.Download(code).ToString(), StringComparison.Ordinal);
    }
}
