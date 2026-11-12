import { useEffect, useState } from 'react';
import { useApp, errorText } from '../state';
import { Feedback } from '../components/Media';
import { ActionStatus, useAction } from './Controls';
import { importQueueApi } from './importerApi';
import type { ImportQueue } from './importerApi';

export default function ImportJobs() {
  const { api } = useApp();
  const [queue, setQueue] = useState<ImportQueue>();
  const [error, setError] = useState('');
  const [url, setUrl] = useState('');
  const [title, setTitle] = useState('');
  const [library, setLibrary] = useState('');
  const [rights, setRights] = useState('');
  const [confirmed, setConfirmed] = useState(false);
  const action = useAction();
  useEffect(() => {
    let active = true;
    let loading = false;
    async function refresh() {
      if (loading) return;
      loading = true;
      try {
        const state = await importQueueApi(api).state();
        if (active) {
          setQueue(state);
          setError('');
        }
      } catch (e) {
        if (active) setError(errorText(e));
      } finally {
        loading = false;
      }
    }
    void refresh();
    const timer = window.setInterval(() => {
      if (document.visibilityState !== 'hidden') void refresh();
    }, 3000);
    return () => {
      active = false;
      window.clearInterval(timer);
    };
  }, [api]);
  const destinationId = library || queue?.destinations[0]?.id || '';
  async function refresh() {
    setQueue(await importQueueApi(api).state());
  }
  return (
    <section className="panel">
      <h2>Import a video</h2>
      <Feedback busy={!queue && !error} error={error} />
      {queue && (
        <>
          <p>{queue.message}</p>
          <p>
            Choose a direct HTTPS MP4 source you are authorized to retain. Playlists, channels,
            other formats and scheduled sources are not available in this preview.
          </p>
          {!queue.destinations.length && (
            <p>
              Create a Videos / Home Videos library with one local folder and secure write
              permissions in Libraries & metadata first.
            </p>
          )}
          <form
            className="settings-fields"
            onSubmit={(event) => {
              event.preventDefault();
              void action.run(async () => {
                await importQueueApi(api).submit({
                  url,
                  title,
                  destinationId,
                  rights,
                  rightsConfirmed: confirmed,
                });
                setUrl('');
                setTitle('');
                setRights('');
                setConfirmed(false);
                await refresh();
              }, 'Import saved on the server. You can leave this page; progress survives a server restart.');
            }}
          >
            <label>
              Source URL
              <input
                type="url"
                required
                autoComplete="off"
                maxLength={4096}
                value={url}
                onChange={(e) => setUrl(e.target.value)}
              />
            </label>
            <label>
              Imported title
              <input
                required
                maxLength={200}
                value={title}
                onChange={(e) => setTitle(e.target.value)}
              />
            </label>
            <label>
              Destination library
              <select required value={destinationId} onChange={(e) => setLibrary(e.target.value)}>
                {!queue.destinations.length && <option value="">No eligible library</option>}
                {queue.destinations.map((item) => (
                  <option key={item.id} value={item.id}>
                    {item.name}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Rights / permission record
              <input
                required
                maxLength={2000}
                value={rights}
                onChange={(e) => setRights(e.target.value)}
                placeholder="For example: my own export or license permission"
              />
            </label>
            <label>
              <input
                type="checkbox"
                required
                checked={confirmed}
                onChange={(e) => setConfirmed(e.target.checked)}
              />
              I have the rights and permission to download and retain this source.
            </label>
            <button
              type="submit"
              disabled={action.busy || !queue.available || !destinationId || !confirmed}
            >
              Queue authorized video
            </button>
          </form>
          <ActionStatus message={action.message} />
          <h3>Queue & history</h3>
          {!queue.jobs.length && <p>No imports yet.</p>}
          <ul className="settings-fields">
            {queue.jobs.map((job) => (
              <li key={job.id} className="panel">
                <strong>{job.title}</strong>
                <p>
                  {job.sourceHost} ·{' '}
                  {job.state === 'completed' ? 'Imported · library scan requested' : job.state} ·{' '}
                  {Math.round(job.bytes / 1024)} KiB · attempt {job.attempts}/3
                </p>
                <small>Job {job.id}</small>
                {job.error && (
                  <p>
                    Import failed ({job.error}). Check the server import diagnostics using this job
                    ID.
                  </p>
                )}
                {['queued', 'running'].includes(job.state) && (
                  <button
                    disabled={action.busy}
                    aria-label={`Cancel ${job.title}`}
                    onClick={() =>
                      void action.run(async () => {
                        await importQueueApi(api).cancel(job.id);
                        await refresh();
                      }, 'Cancellation saved.')
                    }
                  >
                    Cancel
                  </button>
                )}
                {['failed', 'cancelled'].includes(job.state) && job.attempts < 3 && (
                  <button
                    disabled={action.busy}
                    aria-label={`Retry ${job.title}`}
                    onClick={() =>
                      void action.run(async () => {
                        await importQueueApi(api).retry(job.id);
                        await refresh();
                      }, 'Retry saved.')
                    }
                  >
                    Retry
                  </button>
                )}
              </li>
            ))}
          </ul>
        </>
      )}
    </section>
  );
}
