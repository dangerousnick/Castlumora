using System;

namespace Jellyfin.Api.Castlumora;

/// <summary>A permission-filtered snapshot of one followed show's current episodes.</summary>
/// <param name="SeriesId">Visible followed series.</param>
/// <param name="EpisodeIds">Visible native episode IDs.</param>
public sealed record EpisodeNotificationSnapshot(Guid SeriesId, Guid[] EpisodeIds);
