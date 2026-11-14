import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Radio, Play } from 'lucide-react';
import { useApp, useLoad } from '../state';
import { Feedback, Poster } from '../components/Media';
import { ActionStatus, SettingsEditor, useAction } from '../admin/Controls';
import { liveApi } from '../live/api';
import type { Timer } from '../live/api';
import { RecordingControls } from '../live/RecordingControls';
type Tab = 'Guide' | 'Recordings' | 'Scheduled' | 'Series';
export default function Live() {
  const { api, session } = useApp();
  const live = liveApi(api, session.user.Id);
  const [tab, setTab] = useState<Tab>('Guide');
  const [refresh, setRefresh] = useState(0);
  const [offset, setOffset] = useState(0);
  const [channel, setChannel] = useState('');
  const [start, setStart] = useState(() => new Date().toISOString().slice(0, 16));
  const [editing, setEditing] = useState<{ timer: Timer; series: boolean }>();
  const action = useAction();
  const canManage =
    session.user.Policy?.IsAdministrator || session.user.Policy?.EnableLiveTvManagement;
  const channels = useLoad(() => api.channels(), [api]);
  const result = useLoad(async () => {
    if (tab === 'Guide') {
      const from = new Date(start + 'Z');
      return live.programs(
        from.toISOString(),
        new Date(+from + 6 * 3600000).toISOString(),
        channel,
        offset,
      );
    }
    if (tab === 'Recordings') return live.recordings(offset);
    return { Items: [], TotalRecordCount: 0 };
  }, [api, session.user.Id, tab, refresh, start, channel, offset]);
  const timers = useLoad(
    async () =>
      tab === 'Scheduled' || tab === 'Series' ? live.timers(tab === 'Series') : { Items: [] },
    [api, session.user.Id, tab, refresh],
  );
  const reload = () => setRefresh((v) => v + 1);
  useEffect(() => {
    if (tab === 'Guide') return;
    const timer = window.setInterval(() => {
      if (!document.hidden) setRefresh((value) => value + 1);
    }, 10000);
    return () => window.clearInterval(timer);
  }, [tab]);
  return (
    <main className="page">
      <div className="page-heading">
        <div>
          <span className="eyebrow">
            <Radio size={15} /> LIVE TV & DVR
          </span>
          <h1>Live from your world</h1>
          <p>Your channels, guide and recordings.</p>
        </div>
        {session.user.Policy?.IsAdministrator && (
          <Link className="button secondary" to="/admin/live">
            Tuner & guide setup
          </Link>
        )}
      </div>
      <nav className="admin-tabs" aria-label="Live TV views">
        {(['Guide', 'Recordings', 'Scheduled', 'Series'] as Tab[]).map((t) => (
          <button
            key={t}
            aria-pressed={tab === t}
            onClick={() => {
              setTab(t);
              setOffset(0);
              setEditing(undefined);
            }}
          >
            {t}
          </button>
        ))}
      </nav>
      <button onClick={reload}>Refresh guide & recordings</button>
      <ActionStatus message={action.message} />
      <Feedback
        busy={result.busy || timers.busy}
        error={result.error || timers.error || channels.error}
      />
      {tab === 'Guide' && (
        <>
          <div className="admin-fields">
            <label>
              Guide start (UTC)
              <input
                type="datetime-local"
                value={start}
                onChange={(e) => {
                  setStart(e.target.value);
                  setOffset(0);
                }}
              />
            </label>
            <label>
              Channel
              <select
                value={channel}
                onChange={(e) => {
                  setChannel(e.target.value);
                  setOffset(0);
                }}
              >
                <option value="">All channels</option>
                {channels.value?.Items.map((c) => (
                  <option key={c.Id} value={c.Id}>
                    {c.Name}
                  </option>
                ))}
              </select>
            </label>
          </div>
          {channels.value?.Items.length === 0 && (
            <div className="empty">
              <h2>No channels available</h2>
              <p>
                Ask an administrator to configure tuners and guide providers, or check your profile
                permissions.
              </p>
            </div>
          )}
          <div className="guide">
            {channels.value?.Items.filter((c) => !channel || c.Id === channel).map((c) => (
              <article key={c.Id}>
                <Link to={'/play/' + c.Id} className="channel">
                  <div>{api.image(c) ? <img src={api.image(c)} alt="" /> : <Radio />}</div>
                  <span>
                    <strong>{c.Name}</strong>
                    <small>
                      <Play size={12} /> Watch live
                    </small>
                  </span>
                </Link>
                <div className="programs">
                  {result.value?.Items.filter((p) => p.ChannelId === c.Id).map((p) => (
                    <details key={p.Id}>
                      <summary>
                        <small>
                          {p.StartDate &&
                            new Date(p.StartDate).toLocaleTimeString([], {
                              hour: '2-digit',
                              minute: '2-digit',
                            })}
                        </small>
                        <strong>{p.Name}</strong>
                      </summary>
                      <p>{p.Overview ?? 'No program description available.'}</p>
                      <Link to={'/play/' + c.Id}>Watch channel</Link>
                      {canManage && (
                        <button
                          disabled={action.busy}
                          onClick={() =>
                            void action.run(async () => {
                              setEditing({ timer: await live.defaults(p.Id), series: false });
                            }, 'Review recording options below.')
                          }
                        >
                          Record options
                        </button>
                      )}
                    </details>
                  ))}
                </div>
              </article>
            ))}
          </div>
        </>
      )}
      {tab === 'Recordings' && (
        <div className="poster-grid">
          {result.value?.Items.map((item) => (
            <div key={item.Id}>
              <Poster item={item} />
              <RecordingControls item={item} changed={reload} />
            </div>
          ))}
        </div>
      )}
      {(tab === 'Scheduled' || tab === 'Series') && (
        <div className="admin-cards">
          {timers.value?.Items.map((timer) => (
            <article className="panel" key={timer.Id}>
              <h2>{timer.Name}</h2>
              <p>
                {timer.ChannelName} {timer.StartDate && new Date(timer.StartDate).toLocaleString()}
              </p>
              <p>{timer.Status}</p>
              {canManage && (
                <>
                  <button onClick={() => setEditing({ timer, series: tab === 'Series' })}>
                    Edit recording
                  </button>
                  <button
                    disabled={action.busy}
                    onClick={() => {
                      if (
                        timer.Id &&
                        window.confirm(
                          tab === 'Series'
                            ? 'Cancel this series and its scheduled recordings?'
                            : timer.Status === 'InProgress'
                              ? 'Stop this recording now? Any recorded media is retained; an empty recording is removed.'
                              : 'Cancel this scheduled recording?',
                        )
                      )
                        void action.run(
                          async () => {
                            await live.cancel(timer.Id!, tab === 'Series');
                            reload();
                          },
                          timer.Status === 'InProgress' && tab !== 'Series'
                            ? 'Stop requested. Any recorded media is retained.'
                            : 'Recording schedule cancelled.',
                        );
                    }}
                  >
                    {timer.Status === 'InProgress' && tab !== 'Series'
                      ? 'Stop recording'
                      : 'Cancel recording'}
                  </button>
                </>
              )}
            </article>
          ))}
        </div>
      )}
      {!result.busy &&
        !timers.busy &&
        tab !== 'Guide' &&
        !result.value?.Items.length &&
        !timers.value?.Items.length && <p className="empty">No {tab.toLowerCase()} available.</p>}
      {(tab === 'Guide' || tab === 'Recordings') && (
        <div className="pagination">
          <button
            disabled={offset === 0}
            onClick={() => setOffset(Math.max(0, offset - (tab === 'Guide' ? 100 : 48)))}
          >
            Previous page
          </button>
          <span>{result.value?.TotalRecordCount ?? 0} results</span>
          <button
            disabled={
              !result.value || offset + result.value.Items.length >= result.value.TotalRecordCount
            }
            onClick={() => setOffset(offset + (tab === 'Guide' ? 100 : 48))}
          >
            Next page
          </button>
        </div>
      )}
      {editing && canManage && (
        <section>
          <h2>Recording options</h2>
          {!editing.timer.Id && (
            <label>
              <input
                type="checkbox"
                checked={editing.series}
                onChange={(e) => setEditing({ ...editing, series: e.target.checked })}
              />{' '}
              Record the series
            </label>
          )}
          <SettingsEditor
            key={`${editing.timer.Id ?? editing.timer.ProgramId}-${editing.series}`}
            title={editing.series ? 'Series recording' : 'Single recording'}
            value={editing.timer}
            fields={[
              { key: 'Name', label: 'Name' },
              { key: 'PrePaddingSeconds', label: 'Padding before (seconds)', type: 'number' },
              { key: 'PostPaddingSeconds', label: 'Padding after (seconds)', type: 'number' },
              ...(editing.series
                ? [
                    { key: 'RecordNewOnly', label: 'Only new episodes', type: 'boolean' as const },
                    { key: 'RecordAnyTime', label: 'Any time', type: 'boolean' as const },
                    { key: 'RecordAnyChannel', label: 'Any channel', type: 'boolean' as const },
                    {
                      key: 'KeepUpTo',
                      label: 'Keep up to (0 = unlimited)',
                      type: 'number' as const,
                    },
                  ]
                : []),
            ]}
            save={async (value) => {
              await live.save(value, editing.series);
              reload();
              setEditing(undefined);
            }}
          />
          <button onClick={() => setEditing(undefined)}>Close options</button>
        </section>
      )}
    </main>
  );
}
