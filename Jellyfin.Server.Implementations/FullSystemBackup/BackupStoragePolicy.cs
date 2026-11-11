using System;
using Jellyfin.Server.Implementations.StorageHelpers;

namespace Jellyfin.Server.Implementations.FullSystemBackup;

/// <summary>Per-service backup reserve; production construction retains the existing safety requirement.</summary>
internal sealed class BackupStoragePolicy
{
    internal const long ProductionMinimumFreeBytes = 5_368_709_115;
    private readonly Func<string, long> _getFreeBytes;
    private readonly long _minimumFreeBytes;

    internal BackupStoragePolicy(Func<string, long>? getFreeBytes = null, long minimumFreeBytes = ProductionMinimumFreeBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimumFreeBytes);
        _getFreeBytes = getFreeBytes ?? (path => StorageHelper.GetFreeSpaceOf(path).FreeSpace);
        _minimumFreeBytes = minimumFreeBytes;
    }

    internal void Validate(string path)
    {
        // Unknown capacity (-1) fails closed just like the original production guard.
        if (_getFreeBytes(path) < _minimumFreeBytes)
        {
            throw new InvalidOperationException($"The backup directory '{path}' does not have at least '{StorageHelper.HumanizeStorageSize(_minimumFreeBytes)}' free space. Cannot create backup.");
        }
    }
}
