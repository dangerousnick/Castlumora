using System;
using System.Linq;
using Jellyfin.Api.Castlumora;
using Xunit;

namespace Jellyfin.Api.Tests.Castlumora;

public class AudioFingerprintTests
{
    [Fact]
    public void RepeatedSequence_RequiresTwoPeersAndSpectralChanges()
    {
        var random = new Random(42);
        var theme = Enumerable.Range(0, 40).Select(_ => (ulong?)((ulong)random.NextInt64() ^ ((ulong)random.NextInt64() << 1))).ToArray();
        ulong?[] target = [null, null, .. theme, null];
        ulong?[] peer = [null, .. theme, null, null];
        Assert.Null(AudioFingerprint.FindRepeated(target, [peer]));
        Assert.Equal((1.0, 21.0), AudioFingerprint.FindRepeated(target, [peer, peer]));
        Assert.Null(AudioFingerprint.FindRepeated(Enumerable.Repeat<ulong?>(0x5555555555555555, 100).ToArray(), [Enumerable.Repeat<ulong?>(0x5555555555555555, 100).ToArray(), Enumerable.Repeat<ulong?>(0x5555555555555555, 100).ToArray()]));
    }

    [Fact]
    public void DecodedAudio_LandmarksRecognizeAChangingRepeatedTheme()
    {
        var random = new Random(71);
        var pcm = new byte[4000 * 2 * 40];
        for (var frame = 0; frame < 40; frame++)
        {
            var frequency = 180 + random.Next(2500);
            for (var sample = 0; sample < 4000; sample++)
            {
                var value = (short)(Math.Sin(2 * Math.PI * frequency * sample / 8000) * 18000);
                var offset = ((frame * 4000) + sample) * 2;
                pcm[offset] = (byte)value;
                pcm[offset + 1] = (byte)(value >> 8);
            }
        }

        var prints = AudioFingerprint.Compute(pcm);
        Assert.NotNull(AudioFingerprint.FindRepeated(prints, [prints, prints]));
    }

    [Fact]
    public void Silence_ProducesNoUsefulLandmarks()
    {
        Assert.All(AudioFingerprint.Compute(new byte[16000]), value => Assert.Null(value));
    }
}
