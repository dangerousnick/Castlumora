import { useState } from 'react';
import type { Layout } from '../config/schema';
import { useApp, useLoad, errorText } from '../state';
import { Feedback } from './Media';
export function PresentationHistory({
  draft,
  dirty,
  onRestore,
}: {
  draft: Layout;
  dirty: boolean;
  onRestore: (config: Layout) => void;
}) {
  const { api } = useApp();
  const [selected, setSelected] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const result = useLoad(() => api.history(), [api, draft.revision]);
  async function restore() {
    setBusy(true);
    try {
      onRestore(await api.restore(Number(selected), draft.revision));
      setSelected('');
      setError('');
    } catch (e) {
      setError(errorText(e));
    } finally {
      setBusy(false);
    }
  }
  return (
    <section className="panel">
      <h3>Publication history & rollback</h3>
      <p>
        Restore a saved publication into the draft. Your current published layout stays live until
        you publish again.
      </p>
      <Feedback busy={result.busy} error={result.error || error} />
      <div className="filters">
        <label>
          Published revision
          <select
            aria-label="Published revision"
            value={selected}
            onChange={(e) => setSelected(e.target.value)}
          >
            <option value="">Choose a revision…</option>
            {[...(result.value ?? [])].reverse().map((entry) => (
              <option key={entry.revision} value={entry.revision}>
                Revision {entry.revision} · {new Date(entry.publishedAt).toLocaleString()}
              </option>
            ))}
          </select>
        </label>
        <button disabled={dirty || busy || !selected} onClick={() => void restore()}>
          Restore to draft
        </button>
      </div>
      {dirty && <small>Save your current changes before restoring another revision.</small>}
    </section>
  );
}
