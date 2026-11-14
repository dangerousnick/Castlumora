using System;
using System.Collections.Generic;
using System.Numerics;

namespace Jellyfin.Api.Castlumora;

/// <summary>Local audio landmarks for conservative repeated-sequence suggestions.</summary>
public static class AudioFingerprint
{
    /// <summary>Converts 8kHz mono signed little-endian PCM into half-second spectral landmarks.</summary>
    /// <param name="pcm">Decoded PCM bytes.</param>
    /// <returns>Landmarks; null marks silence or insufficient spectral diversity.</returns>
    public static ulong?[] Compute(ReadOnlySpan<byte> pcm)
    {
        const int samples = 4000;
        var result = new ulong?[pcm.Length / (samples * 2)];
        var energies = new double[16];
        for (var frame = 0; frame < result.Length; frame++)
        {
            var energy = 0.0;
            for (var band = 0; band < energies.Length; band++)
            {
                var coefficient = 2 * Math.Cos(2 * Math.PI * (150 * Math.Pow(20, band / 15.0)) / 8000);
                var previous = 0.0;
                var beforePrevious = 0.0;
                for (var sample = 0; sample < samples; sample++)
                {
                    var offset = ((frame * samples) + sample) * 2;
                    var value = (short)(pcm[offset] | (pcm[offset + 1] << 8)) / 32768.0;
                    var next = value + (coefficient * previous) - beforePrevious;
                    beforePrevious = previous;
                    previous = next;
                    if (band == 0)
                    {
                        energy += value * value;
                    }
                }

                energies[band] = Math.Log(1 + Math.Max(0, (previous * previous) + (beforePrevious * beforePrevious) - (coefficient * previous * beforePrevious)));
            }

            if (energy / samples < 0.00001)
            {
                continue;
            }

            // Comparisons of bands are independent of overall loudness; reject flat, uninformative frames.
            ulong fingerprint = 0;
            for (var bit = 0; bit < 64; bit++)
            {
                var left = bit % 16;
                var right = (left + 1 + (bit / 16)) % 16;
                if (energies[left] > energies[right])
                {
                    fingerprint |= 1UL << bit;
                }
            }

            var ones = BitOperations.PopCount(fingerprint);
            if (ones is > 8 and < 56)
            {
                result[frame] = fingerprint;
            }
        }

        return result;
    }

    /// <summary>Finds a continuous repeated passage corroborated by at least two other episodes.</summary>
    /// <param name="target">Target half-second landmarks.</param>
    /// <param name="peers">Other episodes in the same season.</param>
    /// <returns>Suggested start/end seconds, or null when evidence is insufficient.</returns>
    public static (double Start, double End)? FindRepeated(ulong?[] target, IReadOnlyList<ulong?[]> peers)
    {
        if (peers.Count < 2)
        {
            return null;
        }

        var votes = new int[target.Length];
        foreach (var peer in peers)
        {
            var coverage = new int[target.Length + 1];
            var previous = new int[peer.Length + 1];
            for (var i = 0; i < target.Length; i++)
            {
                var current = new int[peer.Length + 1];
                for (var j = 0; j < peer.Length; j++)
                {
                    if (target[i].HasValue && peer[j].HasValue && BitOperations.PopCount(target[i]!.Value ^ peer[j]!.Value) <= 6)
                    {
                        current[j + 1] = previous[j] + 1;
                        if (current[j + 1] >= 30)
                        {
                            coverage[i - current[j + 1] + 1]++;
                            coverage[i + 1]--;
                        }
                    }
                }

                previous = current;
            }

            var overlaps = 0;
            for (var i = 0; i < votes.Length; i++)
            {
                overlaps += coverage[i];
                if (overlaps > 0)
                {
                    votes[i]++;
                }
            }
        }

        var bestStart = 0;
        var bestLength = 0;
        var run = 0;
        for (var i = 0; i < votes.Length; i++)
        {
            run = votes[i] >= 2 ? run + 1 : 0;
            if (run > bestLength)
            {
                bestLength = run;
                bestStart = i - run + 1;
            }
        }

        if (bestLength < 30)
        {
            return null;
        }

        // Constant tones can match indefinitely; require changing landmarks in the proposed interval.
        var distinct = new HashSet<ulong>();
        for (var i = bestStart; i < bestStart + bestLength; i++)
        {
            if (target[i].HasValue)
            {
                distinct.Add(target[i]!.Value);
            }
        }

        return distinct.Count < 8 ? null : (bestStart * 0.5, (bestStart + bestLength) * 0.5);
    }
}
