import { useState } from 'react';
import { useApp, useLoad } from '../state';
import { Feedback } from '../components/Media';
import { ActionStatus, useAction } from './Controls';
import { importerApi } from './importerApi';
import type { ImporterRuntime } from './importerApi';

export default function Imports() {
  const { api } = useApp();
  const initial = useLoad(() => importerApi(api).status(), [api]);
  const [current, setCurrent] = useState<ImporterRuntime>();
  const action = useAction();
  const runtime = current ?? initial.value;
  const busy = initial.busy || action.busy;
  return (
    <div className="page">
      <h1>Imports & Sources</h1>
      <p>
        This preview provides the managed media-import runtime. URL importing, queues and scheduled
        sources are not available yet.
      </p>
      <Feedback busy={busy} error={current ? '' : initial.error} />
      {runtime && (
        <section className="panel">
          <h2>yt-dlp runtime</h2>
          <dl>
            <dt>Status</dt>
            <dd>{runtime.ready ? 'Ready' : runtime.state}</dd>
            <dt>Reviewed version</dt>
            <dd>{runtime.expectedVersion}</dd>
            <dt>Platform</dt>
            <dd>{runtime.platform}</dd>
          </dl>
          <p>{runtime.message}</p>
          {!runtime.supported && <p>This platform has no managed executable in this preview.</p>}
          {runtime.supported && !runtime.bundleAvailable && (
            <p>
              The bundled asset is missing. Use the complete Castlumora preview package; no separate
              plugin is needed.
            </p>
          )}
          <button
            disabled={busy || !runtime.supported || !runtime.bundleAvailable}
            onClick={() => {
              if (
                window.confirm(
                  `Install the bundled, verified yt-dlp ${runtime.expectedVersion}? Existing versions are retained for rollback with their matching Castlumora package.`,
                )
              )
                void action.run(
                  async () => setCurrent(await importerApi(api).install()),
                  'Managed runtime installed and checked.',
                );
            }}
          >
            {runtime.ready ? 'Reinstall pinned runtime' : 'Install bundled runtime'}
          </button>
          <details>
            <summary>Integrity and licensing</summary>
            <p>Expected SHA-256: {runtime.expectedSha256 ?? 'Unavailable for this platform'}</p>
            <p>
              Updates are reviewed and shipped with Castlumora packages. No automatic updater or
              custom executable is enabled. Tool licenses and source materials are included with the
              package.
            </p>
            <a href={runtime.releaseUrl} target="_blank" rel="noopener noreferrer">
              Official pinned release
            </a>
          </details>
        </section>
      )}
      <button
        disabled={busy}
        onClick={() =>
          void action.run(
            async () => setCurrent(await importerApi(api).status()),
            'Runtime health checked on the server.',
          )
        }
      >
        Check runtime
      </button>
      <ActionStatus message={action.message} />
    </div>
  );
}
