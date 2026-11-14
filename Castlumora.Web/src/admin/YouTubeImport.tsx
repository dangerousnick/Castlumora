import { useState } from 'react';
import { useApp } from '../state';
import { ActionStatus, useAction } from './Controls';
import { importQueueApi } from './importerApi';
import type { YouTubePreview } from './importerApi';

export default function YouTubeImport({
  destinations,
  onQueued,
}: {
  destinations: { id: string; name: string }[];
  onQueued: () => Promise<void>;
}) {
  const { api } = useApp();
  const [url, setUrl] = useState('');
  const [library, setLibrary] = useState('');
  const [height, setHeight] = useState(720);
  const [size, setSize] = useState(128);
  const [preview, setPreview] = useState<YouTubePreview>();
  const [title, setTitle] = useState('');
  const [rights, setRights] = useState('');
  const [confirmed, setConfirmed] = useState(false);
  const [automaticRetries, setAutomaticRetries] = useState(false);
  const action = useAction();
  const destinationId = library || destinations[0]?.id || '';
  return (
    <section aria-label="Authorized YouTube import">
      <h3>Import a YouTube video</h3>
      <p>
        Preview one video you own or have permission to retain. Downloads run on your server;
        sign-in requirements, DRM and source restrictions are never bypassed. Playlists and channels
        are not available here.
      </p>
      <form
        className="settings-fields"
        onSubmit={(event) => {
          event.preventDefault();
          setPreview(undefined);
          void action.run(async () => {
            const result = await importQueueApi(api).previewYouTube({
              url,
              destinationId,
              maximumHeight: height,
              maximumMiB: size,
            });
            setPreview(result);
            setTitle(result.title);
            setConfirmed(false);
          }, 'Preview ready. Review the creator, destination and your permission before queueing.');
        }}
      >
        <label>
          YouTube video URL
          <input
            type="url"
            disabled={action.busy}
            required
            maxLength={4096}
            value={url}
            onChange={(event) => {
              setUrl(event.target.value);
              setPreview(undefined);
            }}
          />
        </label>
        <label>
          YouTube destination library
          <select
            disabled={action.busy}
            required
            value={destinationId}
            onChange={(event) => {
              setLibrary(event.target.value);
              setPreview(undefined);
            }}
          >
            {!destinations.length && <option value="">No eligible library</option>}
            {destinations.map((item) => (
              <option key={item.id} value={item.id}>
                {item.name}
              </option>
            ))}
          </select>
        </label>
        <label>
          Maximum video quality
          <select
            disabled={action.busy}
            value={height}
            onChange={(event) => {
              setHeight(Number(event.target.value));
              setPreview(undefined);
            }}
          >
            {[360, 480, 720, 1080].map((value) => (
              <option key={value} value={value}>
                {value}p
              </option>
            ))}
          </select>
        </label>
        <label>
          Maximum import size
          <select
            disabled={action.busy}
            value={size}
            onChange={(event) => {
              setSize(Number(event.target.value));
              setPreview(undefined);
            }}
          >
            {[32, 128, 256, 512].map((value) => (
              <option key={value} value={value}>
                {value} MiB
              </option>
            ))}
          </select>
        </label>
        <button disabled={action.busy || !destinationId}>Preview YouTube video</button>
      </form>
      {preview && (
        <div className="settings-fields">
          <p>
            <strong>{preview.title}</strong> — {preview.creator} · {preview.videoId} ·{' '}
            {preview.height}p
          </p>
          <p>{preview.description}</p>
          <p>
            Budget: {preview.maximumBytes / 1048576} MiB. Confirmation expires{' '}
            {new Date(preview.expiresAt).toLocaleString()}. Queue progress survives server restarts.
            One worker, a ten-minute attempt limit and the selected size budget apply.
          </p>
          <label>
            YouTube imported title
            <input
              maxLength={200}
              required
              value={title}
              onChange={(event) => setTitle(event.target.value)}
            />
          </label>
          <label>
            YouTube rights / permission record
            <input
              maxLength={2000}
              required
              value={rights}
              onChange={(event) => setRights(event.target.value)}
            />
          </label>
          <label>
            <input
              type="checkbox"
              checked={confirmed}
              onChange={(event) => setConfirmed(event.target.checked)}
            />
            I have permission to download and retain this YouTube video and its metadata.
          </label>
          <label>
            <input
              type="checkbox"
              checked={automaticRetries}
              onChange={(event) => setAutomaticRetries(event.target.checked)}
            />
            Retry temporary network failures within the three-attempt budget
          </label>
          <button
            type="button"
            disabled={action.busy || !confirmed || !rights.trim() || !title.trim()}
            onClick={() =>
              void action.run(async () => {
                await importQueueApi(api).submitYouTube({
                  previewId: preview.previewId,
                  destinationId,
                  title,
                  rights,
                  rightsConfirmed: confirmed,
                  automaticRetries,
                });
                setPreview(undefined);
                setUrl('');
                setRights('');
                setConfirmed(false);
                setAutomaticRetries(false);
                await onQueued();
              }, 'YouTube import saved. Monitor, cancel or retry it in the shared queue below.')
            }
          >
            Queue confirmed YouTube video
          </button>
        </div>
      )}
      <ActionStatus message={action.message} />
    </section>
  );
}
