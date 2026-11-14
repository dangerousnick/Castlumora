using System.IO;
using Jellyfin.Api.Castlumora;
using Xunit;

namespace Jellyfin.Api.Tests.Castlumora;

public sealed class YouTubeVideoSourceTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=Abc_def-123")]
    [InlineData("https://youtube.com/watch?v=Abc_def-123&t=30&list=PL_context&index=2")]
    [InlineData("https://m.youtube.com/watch?feature=share&v=Abc_def-123")]
    [InlineData("https://youtu.be/Abc_def-123?si=tracking")]
    [InlineData("https://www.youtube.com/shorts/Abc_def-123")]
    [InlineData("https://www.youtube.com/live/Abc_def-123")]
    [InlineData("https://www.youtube.com/embed/Abc_def-123")]
    public void EquivalentSingleVideoLinks_HaveOneStableIdentity(string source)
    {
        var parsed = YouTubeVideoSource.Parse(source);
        Assert.Equal("Abc_def-123", parsed.VideoId);
        Assert.Equal("youtube:Abc_def-123", parsed.StableKey);
        Assert.Equal("https://www.youtube.com/watch?v=Abc_def-123", parsed.CanonicalUrl);
    }

    [Theory]
    [InlineData("http://www.youtube.com/watch?v=Abc_def-123")]
    [InlineData("https://www.youtube.com:444/watch?v=Abc_def-123")]
    [InlineData("https://user:secret@www.youtube.com/watch?v=Abc_def-123")]
    [InlineData("https://youtube.com.evil.invalid/watch?v=Abc_def-123")]
    [InlineData("https://youtube.com./watch?v=Abc_def-123")]
    [InlineData("https://127.0.0.1/watch?v=Abc_def-123")]
    [InlineData("https://www.youtube.com/playlist?list=PL_only")]
    [InlineData("https://www.youtube.com/@creator")]
    [InlineData("https://www.youtube.com/watch?v=Abc_def-123&v=OtherId-123")]
    [InlineData("https://www.youtube.com/watch?v=too-short")]
    [InlineData("https://youtu.be/Abc_def-123/extra")]
    [InlineData("https://youtu.be/Abc_def-123?v=OtherId-123")]
    [InlineData("https://www.youtube.com/watch?v=Abc_def-123#fragment")]
    [InlineData("https://www.youtube.com/watch?v=Abc_def-123&cookies=secret")]
    [InlineData("https://www.youtube.com/watch?%76=Abc_def-123")]
    [InlineData("https://www.youtube.com/watch?v=Abc_def%2F12")]
    public void UnsafeAmbiguousOrUnsupportedLinks_AreRejected(string source)
    {
        Assert.Throws<InvalidDataException>(() => YouTubeVideoSource.Parse(source));
    }
}
