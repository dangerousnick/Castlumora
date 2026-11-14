import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ApiError } from '../api/client';
import { useApp, useLoad } from '../state';
import { Feedback } from '../components/Media';
import { MediaPicker } from '../components/MediaPicker';
import { ActionStatus, useAction } from '../admin/Controls';
import { musicApi } from './api';
import { useMusic } from './MusicProvider';
import type { Item } from '../api/types';
type MusicTab = 'Albums' | 'Artists' | 'Tracks' | 'Playlists';
export default function Music() {
  const { api, session } = useApp();
  const [tab, setTab] = useState<MusicTab>('Albums');
  const [query, setQuery] = useState('');
  const [offset, setOffset] = useState(0);
  const [name, setName] = useState('');
  const navigate = useNavigate();
  const action = useAction();
  const result = useLoad(
    () =>
      tab === 'Artists'
        ? musicApi(api, session.user.Id).artists(offset, query || undefined)
        : api.items({
            IncludeItemTypes: {
              Albums: 'MusicAlbum',
              Tracks: 'Audio',
              Playlists: 'Playlist',
              Artists: 'MusicArtist',
            }[tab],
            StartIndex: offset,
            Limit: 48,
            SearchTerm: query || undefined,
            SortBy: 'SortName',
            SortOrder: 'Ascending',
          }),
    [api, session.user.Id, tab, query, offset],
  );
  return (
    <main className="page">
      <div className="page-heading">
        <div>
          <span className="eyebrow">YOUR EVERYDAY SOUNDTRACK</span>
          <h1>Your music, your rhythm</h1>
          <p>Albums, artists and playlists from your server.</p>
        </div>
        <Link className="button secondary" to="/browse">
          Movies & series
        </Link>
      </div>
      <nav className="admin-tabs" aria-label="Music views">
        {(['Albums', 'Artists', 'Tracks', 'Playlists'] as MusicTab[]).map((value) => (
          <button
            key={value}
            aria-pressed={tab === value}
            onClick={() => {
              setTab(value);
              setOffset(0);
            }}
          >
            {value}
          </button>
        ))}
      </nav>
      <label className="search-box">
        Search music
        <input
          type="search"
          value={query}
          onChange={(e) => {
            setQuery(e.target.value);
            setOffset(0);
          }}
        />
      </label>
      {tab === 'Playlists' && (
        <form
          className="filters"
          onSubmit={(event) => {
            event.preventDefault();
            void action.run(async () => {
              const playlist = await musicApi(api, session.user.Id).createPlaylist(name);
              navigate('/item/' + playlist.Id);
            }, 'Playlist created.');
          }}
        >
          <label>
            New playlist name
            <input required value={name} onChange={(e) => setName(e.target.value)} />
          </label>
          <button disabled={action.busy}>Create music playlist</button>
        </form>
      )}
      <ActionStatus message={action.message} />
      <Feedback busy={result.busy} error={result.error} />
      {result.value &&
        (tab === 'Tracks' ? (
          <TrackList tracks={result.value.Items} />
        ) : (
          <div className="poster-grid">
            {result.value.Items.map((item) => (
              <MusicCard key={item.Id} item={item} />
            ))}
          </div>
        ))}
      {!result.busy && result.value?.Items.length === 0 && (
        <p className="empty">
          No {tab.toLowerCase()} available. Check your libraries and profile access.
        </p>
      )}
      <div className="pagination">
        <button disabled={!offset} onClick={() => setOffset(Math.max(0, offset - 48))}>
          Previous page
        </button>
        <span>{result.value?.TotalRecordCount ?? 0} results</span>
        <button
          disabled={
            !result.value || offset + result.value.Items.length >= result.value.TotalRecordCount
          }
          onClick={() => setOffset(offset + 48)}
        >
          Next page
        </button>
      </div>
    </main>
  );
}
export function MusicDetail({ item }: { item: Item }) {
  const { api, session } = useApp();
  const music = useMusic();
  const [refresh, setRefresh] = useState(0);
  const [offset, setOffset] = useState(0);
  const [name, setName] = useState(item.Name);
  const [title, setTitle] = useState(item.Name);
  const action = useAction();
  const navigate = useNavigate();
  const playlist = item.Type === 'Playlist';
  const artist = item.Type === 'MusicArtist';
  const audio = item.Type === 'Audio';
  const result = useLoad(async () => {
    const adapter = musicApi(api, session.user.Id);
    const page = audio
      ? { Items: [item], TotalRecordCount: 1 }
      : artist
        ? await adapter.albums(item.Id, offset)
        : playlist
          ? await adapter.playlist(item.Id, offset)
          : await adapter.tracks(item.Id, offset);
    const permission = playlist ? await adapter.permission(item.Id) : null;
    return { ...page, canEdit: !!permission?.CanEdit };
  }, [api, session.user.Id, item, artist, playlist, audio, offset, refresh]);
  const permission = useLoad(async () => {
    if (!playlist) return null;
    try {
      return await musicApi(api, session.user.Id).permission(item.Id);
    } catch (error) {
      if (error instanceof ApiError && [403, 404].includes(error.status)) return null;
      throw error;
    }
  }, [api, session.user.Id, playlist, item.Id]);
  const reload = () => setRefresh((v) => v + 1);
  return (
    <main className="page music-detail">
      <Link to="/music">← Back to music</Link>
      <div className="music-heading">
        {api.image(item) && <img src={api.image(item)} alt="" />}
        <div>
          <span className="eyebrow">
            {item.Type === 'MusicAlbum' ? 'ALBUM' : item.Type.toUpperCase()}
          </span>
          <h1>{title}</h1>
          <p>
            {item.Artists?.join(', ')} {item.ProductionYear}
          </p>
          <p>{item.Overview}</p>
          {item.AlbumId && <Link to={'/item/' + item.AlbumId}>{item.Album ?? 'View album'}</Link>}
        </div>
      </div>
      <Feedback busy={result.busy} error={result.error} />
      <ActionStatus message={action.message} />
      {artist ? (
        <div className="poster-grid">
          {result.value?.Items.map((album) => (
            <MusicCard key={album.Id} item={album} />
          ))}
        </div>
      ) : (
        <>
          {!!result.value?.Items.length && (
            <div className="actions">
              <button className="primary" onClick={() => music?.play(result.value!.Items)}>
                {audio ? 'Play track' : 'Play these tracks'}
              </button>
              <button onClick={() => music?.enqueue(result.value!.Items)}>
                Add these tracks to queue
              </button>
            </div>
          )}
          <TrackList
            tracks={result.value?.Items ?? []}
            playlist={permission.value?.CanEdit ? item.Id : undefined}
            offset={offset}
            changed={reload}
          />
        </>
      )}
      {playlist && permission.value?.CanEdit && (
        <section className="panel">
          <h2>Edit playlist</h2>
          <form
            className="filters"
            onSubmit={(event) => {
              event.preventDefault();
              void action.run(async () => {
                await musicApi(api, session.user.Id).renamePlaylist(item.Id, name);
                setTitle(name);
              }, 'Playlist renamed.');
            }}
          >
            <label>
              Playlist name
              <input required value={name} onChange={(e) => setName(e.target.value)} />
            </label>
            <button disabled={action.busy}>Rename playlist</button>
          </form>
          <MediaPicker
            label="Add tracks to playlist"
            types="Audio"
            onChoose={(track) =>
              void action.run(async () => {
                await musicApi(api, session.user.Id).add(item.Id, track.Id);
                reload();
              }, 'Track added.')
            }
          />
          <button
            disabled={action.busy}
            onClick={() => {
              if (window.confirm('Delete this playlist? Media files are retained.'))
                void action.run(async () => {
                  await musicApi(api, session.user.Id).deletePlaylist(item.Id);
                  navigate('/music');
                }, 'Playlist deleted.');
            }}
          >
            Delete playlist
          </button>
        </section>
      )}
      {!result.busy && !result.value?.Items.length && (
        <p className="empty">No accessible music here yet.</p>
      )}
      {!audio && (
        <div className="pagination">
          <button
            disabled={!offset}
            onClick={() => setOffset(Math.max(0, offset - (artist ? 48 : 100)))}
          >
            Previous page
          </button>
          <span>{result.value?.TotalRecordCount ?? 0} results</span>
          <button
            disabled={
              !result.value || offset + result.value.Items.length >= result.value.TotalRecordCount
            }
            onClick={() => setOffset(offset + (artist ? 48 : 100))}
          >
            Next page
          </button>
        </div>
      )}
    </main>
  );
}
function MusicCard({ item }: { item: Item }) {
  const { api } = useApp();
  return (
    <Link className="music-card" aria-label={'Details for ' + item.Name} to={'/item/' + item.Id}>
      <div>
        {api.image(item) ? (
          <img src={api.image(item)} alt="" loading="lazy" />
        ) : (
          <span aria-hidden="true">♫</span>
        )}
      </div>
      <strong>{item.Name}</strong>
      <small>{item.Artists?.join(', ') ?? item.Type}</small>
    </Link>
  );
}
function TrackList({
  tracks,
  playlist,
  offset = 0,
  changed,
}: {
  tracks: Item[];
  playlist?: string;
  offset?: number;
  changed?: () => void;
}) {
  const music = useMusic();
  const { api, session } = useApp();
  const action = useAction();
  return (
    <>
      <ActionStatus message={action.message} />
      <div className="track-list">
        {tracks.map((track, index) => (
          <article key={track.PlaylistItemId ?? track.Id}>
            <button
              className="track-play"
              aria-label={'Play ' + track.Name}
              onClick={() => music?.play(tracks, index)}
            >
              ▶
            </button>
            <div>
              <Link to={'/item/' + track.Id}>{track.Name}</Link>
              <small>
                {track.Artists?.join(', ')} {track.Album}
              </small>
            </div>
            <span>
              {track.RunTimeTicks
                ? Math.floor(track.RunTimeTicks / 600000000) +
                  ':' +
                  String(Math.floor(track.RunTimeTicks / 10000000) % 60).padStart(2, '0')
                : ''}
            </span>
            <button aria-label={'Queue ' + track.Name} onClick={() => music?.enqueue([track])}>
              ＋ Queue
            </button>
            {playlist && track.PlaylistItemId && (
              <>
                <button
                  disabled={action.busy || offset + index === 0}
                  aria-label={'Move ' + track.Name + ' up'}
                  onClick={() =>
                    void action.run(async () => {
                      await musicApi(api, session.user.Id).move(
                        playlist,
                        track.PlaylistItemId!,
                        offset + index - 1,
                      );
                      changed?.();
                    }, 'Playlist reordered.')
                  }
                >
                  ↑
                </button>
                <button
                  disabled={action.busy}
                  aria-label={'Remove ' + track.Name + ' from playlist'}
                  onClick={() =>
                    void action.run(async () => {
                      await musicApi(api, session.user.Id).remove(playlist, track.PlaylistItemId!);
                      changed?.();
                    }, 'Track removed from playlist.')
                  }
                >
                  Remove
                </button>
              </>
            )}
          </article>
        ))}
      </div>
    </>
  );
}
