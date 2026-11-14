import { useMemo, useState } from 'react';
import { useApp, useLoad } from '../state';
import { Feedback } from '../components/Media';
import { adminApi } from './api';
import { ActionStatus, SettingsEditor, JsonCollectionEditor, useAction } from './Controls';
export default function System() {
  const { api } = useApp();
  const admin = useMemo(() => adminApi(api), [api]);
  const [tab, setTab] = useState('general');
  const [refresh, setRefresh] = useState(0);
  const [log, setLog] = useState('');
  const action = useAction();
  const general = useLoad(() => admin.config(), [admin]);
  const network = useLoad(
    () => (tab === 'network' ? admin.config('network') : Promise.resolve(undefined)),
    [admin, tab],
  );
  const tasks = useLoad(
    () => (tab === 'tasks' ? admin.tasks() : Promise.resolve([])),
    [admin, tab, refresh],
  );
  const logs = useLoad(
    () =>
      tab === 'logs'
        ? api.request<{ Name: string; Size: number; DateModified: string }[]>('System/Logs')
        : Promise.resolve([]),
    [api, tab],
  );
  const content = useLoad(
    () =>
      tab === 'logs' && log
        ? api.request<string>('System/Logs/Log', {}, { name: log })
        : Promise.resolve(''),
    [api, tab, log],
  );
  const backups = useLoad(
    () =>
      tab === 'backups'
        ? api.request<{ Path: string; DateCreated: string; ServerVersion: string }[]>('Backup')
        : Promise.resolve([]),
    [api, tab, refresh],
  );
  return (
    <>
      <h1>System & operations</h1>
      <nav className="tab-nav">
        {['general', 'network', 'tasks', 'logs', 'backups'].map((t) => (
          <button key={t} className={tab === t ? 'selected' : ''} onClick={() => setTab(t)}>
            {t}
          </button>
        ))}
      </nav>
      <ActionStatus message={action.message} />
      {tab === 'general' && (
        <>
          <Feedback busy={general.busy} error={general.error} />
          {general.value && (
            <SettingsEditor
              title="Server"
              value={general.value}
              fields={[
                { key: 'ServerName', label: 'Server name' },
                { key: 'PreferredMetadataLanguage', label: 'Default metadata language' },
                { key: 'MetadataCountryCode', label: 'Default metadata country' },
                { key: 'UICulture', label: 'Server language' },
                { key: 'EnableMetrics', label: 'Metrics', type: 'boolean' },
              ]}
              save={(value) => admin.saveConfig(value)}
            />
          )}
        </>
      )}
      {tab === 'network' && (
        <>
          <Feedback busy={network.busy} error={network.error} />
          {network.value && (
            <SettingsEditor
              title="Networking"
              value={network.value}
              fields={[
                { key: 'BaseUrl', label: 'Reverse proxy base path' },
                { key: 'InternalHttpPort', label: 'HTTP port', type: 'number' },
                { key: 'InternalHttpsPort', label: 'HTTPS port', type: 'number' },
                { key: 'EnableHttps', label: 'Enable HTTPS', type: 'boolean' },
                { key: 'RequireHttps', label: 'Require HTTPS', type: 'boolean' },
                { key: 'CertificatePath', label: 'Certificate path' },
                { key: 'EnableRemoteAccess', label: 'Remote access', type: 'boolean' },
                { key: 'EnableIPv4', label: 'IPv4', type: 'boolean' },
                { key: 'EnableIPv6', label: 'IPv6', type: 'boolean' },
              ]}
              save={(value) => admin.saveConfig(value, 'network')}
            >
              <p>
                Network changes can require a restart and a new connection address. Keep a working
                administrative connection when changing ports or HTTPS.
              </p>
            </SettingsEditor>
          )}
        </>
      )}
      {tab === 'tasks' && (
        <>
          <Feedback busy={tasks.busy} error={tasks.error} />
          <button onClick={() => setRefresh((r) => r + 1)}>Refresh task status</button>
          {tasks.value?.map((task) => (
            <section className="panel" key={task.Id}>
              <div className="section-heading">
                <div>
                  <h2>{task.Name}</h2>
                  <p>{task.Description}</p>
                </div>
                <span>
                  {task.State}
                  {task.CurrentProgressPercentage !== undefined
                    ? ' · ' + Math.round(task.CurrentProgressPercentage) + '%'
                    : ''}
                </span>
              </div>
              <button
                disabled={action.busy}
                onClick={() =>
                  void action.run(async () => {
                    if (task.State === 'Running') await admin.stopTask(task.Id);
                    else await admin.runTask(task.Id);
                    setRefresh((r) => r + 1);
                  }, 'Task command accepted.')
                }
              >
                {task.State === 'Running' ? 'Cancel task' : 'Run task'}
              </button>
              <details>
                <summary>Scheduling</summary>
                <JsonCollectionEditor
                  title={task.Name + ' triggers'}
                  value={task.Triggers}
                  save={(v) => admin.triggers(task.Id, v as Record<string, unknown>[])}
                />
              </details>
            </section>
          ))}
        </>
      )}
      {tab === 'logs' && (
        <>
          <Feedback busy={logs.busy} error={logs.error} />
          <label>
            Server log
            <select value={log} onChange={(e) => setLog(e.target.value)}>
              <option value="">Select log…</option>
              {logs.value?.map((l) => (
                <option key={l.Name} value={l.Name}>
                  {l.Name} · {Math.round(l.Size / 1024)} KB
                </option>
              ))}
            </select>
          </label>
          <Feedback busy={content.busy} error={content.error} />
          <pre className="server-log">{content.value}</pre>
        </>
      )}
      {tab === 'backups' && (
        <>
          <Feedback busy={backups.busy} error={backups.error} />
          <p>
            Backups use the existing server engine and its free-space checks. Restoring restarts the
            server.
          </p>
          <button
            disabled={action.busy}
            onClick={() =>
              void action.run(async () => {
                await api.request('Backup/Create', { method: 'POST', body: JSON.stringify({}) });
                setRefresh((r) => r + 1);
              }, 'Backup created.')
            }
          >
            Create backup
          </button>
          <div className="admin-list">
            {backups.value?.map((b) => (
              <article key={b.Path}>
                <span>
                  <strong>{new Date(b.DateCreated).toLocaleString()}</strong>
                  <small>
                    {b.Path} · {b.ServerVersion}
                  </small>
                </span>
                <button
                  disabled={action.busy}
                  onClick={() => {
                    if (confirm('Restore this backup and restart the server?'))
                      void action.run(
                        () =>
                          api.request('Backup/Restore', {
                            method: 'POST',
                            body: JSON.stringify({ ArchiveFileName: b.Path.split(/[\\/]/).pop() }),
                          }),
                        'Restore requested; reconnect after restart.',
                      );
                  }}
                >
                  Restore backup
                </button>
              </article>
            ))}
          </div>
        </>
      )}
      {tab === 'general' && (
        <div className="actions">
          <button
            disabled={action.busy}
            onClick={() => {
              if (confirm('Restart this server now? Active streams will be interrupted.'))
                void action.run(
                  () => api.request('System/Restart', { method: 'POST' }),
                  'Restart requested.',
                );
            }}
          >
            Restart server
          </button>
          <button
            disabled={action.busy}
            onClick={() => {
              if (confirm('Shut down this server now?'))
                void action.run(
                  () => api.request('System/Shutdown', { method: 'POST' }),
                  'Shutdown requested.',
                );
            }}
          >
            Shut down
          </button>
        </div>
      )}
    </>
  );
}
