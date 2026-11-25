import type { Api } from '../api/client';
import type { QueryResult } from '../api/types';
export function musicApi(api: Api, userId: string) {
  return {
    artists: (offset = 0, search?: string) =>
      api.request<QueryResult>(
        'Artists/AlbumArtists',
        {},
        {
          UserId: userId,
          StartIndex: offset,
          Limit: 48,
          SearchTerm: search,
          SortBy: 'SortName',
          SortOrder: 'Ascending',
        },
      ),
    albums: (artistId: string, offset = 0) =>
      api.items({
        IncludeItemTypes: 'MusicAlbum',
        AlbumArtistIds: artistId,
        StartIndex: offset,
        Limit: 48,
        SortBy: 'SortName',
      }),
    tracks: (parentId: string, offset = 0) =>
      api.items({
        IncludeItemTypes: 'Audio',
        ParentId: parentId,
        StartIndex: offset,
        Limit: 100,
        SortBy: 'ParentIndexNumber,IndexNumber,SortName',
        SortOrder: 'Ascending',
      }),
    playlist: (id: string, offset = 0) =>
      api.request<QueryResult>(
        `Playlists/${id}/Items`,
        {},
        { UserId: userId, StartIndex: offset, Limit: 100, Fields: 'Overview' },
      ),
    permission: (id: string) =>
      api.request<{ CanEdit: boolean } | null>(`Playlists/${id}/Users/${userId}`),
    createPlaylist: (name: string) =>
      api.request<{ Id: string }>('Playlists', {
        method: 'POST',
        body: JSON.stringify({
          Name: name,
          UserId: userId,
          MediaType: 'Audio',
          IsPublic: false,
          Ids: [],
        }),
      }),
    renamePlaylist: (id: string, name: string) =>
      api.request<void>(`Playlists/${id}`, {
        method: 'POST',
        body: JSON.stringify({ Name: name }),
      }),
    add: (id: string, trackId: string) =>
      api.request<void>(`Playlists/${id}/Items`, { method: 'POST' }, { ids: trackId, userId }),
    remove: (id: string, entryId: string) =>
      api.request<void>(`Playlists/${id}/Items`, { method: 'DELETE' }, { entryIds: entryId }),
    move: (id: string, entryId: string, index: number) =>
      api.request<void>(`Playlists/${id}/Items/${encodeURIComponent(entryId)}/Move/${index}`, {
        method: 'POST',
      }),
    deletePlaylist: (id: string) => api.request<void>(`Items/${id}`, { method: 'DELETE' }),
  };
}
export function audioProfile() {
  const audio = document.createElement('audio');
  const formats = [
    ['mp3', 'audio/mpeg', 'mp3'],
    ['aac', 'audio/aac', 'aac'],
    ['m4a,mp4', 'audio/mp4; codecs="mp4a.40.2"', 'aac'],
    ['flac', 'audio/flac', 'flac'],
    ['ogg', 'audio/ogg; codecs="vorbis"', 'vorbis'],
    ['opus,ogg', 'audio/ogg; codecs="opus"', 'opus'],
    ['wav', 'audio/wav', 'pcm_s16le'],
  ];
  return {
    Name: 'Castlumora Music Browser',
    MaxStreamingBitrate: 20000000,
    MaxStaticBitrate: 100000000,
    DirectPlayProfiles: formats
      .filter(([, mime]) => audio.canPlayType(mime))
      .map(([Container, , AudioCodec]) => ({ Type: 'Audio', Container, AudioCodec })),
    TranscodingProfiles: [
      {
        Type: 'Audio',
        Container: 'mp3',
        AudioCodec: 'mp3',
        Protocol: 'http',
        Context: 'Streaming',
        MaxAudioChannels: '2',
      },
    ],
  };
}
