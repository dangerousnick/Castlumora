import { useMemo, useState } from 'react';
import { useApp, useLoad } from '../state';
import { Feedback } from '../components/Media';
import { adminApi } from './api';
import { ActionStatus, SettingsEditor, JsonCollectionEditor, useAction } from './Controls';
export default function Plugins() {
  const { api } = useApp();
  const admin = useMemo(() => adminApi(api), [api]);
  const [refresh, setRefresh] = useState(0);
  const [selected, setSelected] = useState('');
  const [catalogOpen, setCatalogOpen] = useState(false);
  const action = useAction();
  const plugins = useLoad(() => admin.plugins(), [admin, refresh]);
  const config = useLoad(
    () =>
      selected
        ? api.request<Record<string, unknown>>(`Plugins/${selected}/Configuration`)
        : Promise.resolve(undefined),
    [api, selected],
  );
  const repositories = useLoad(() => api.request<unknown[]>('Repositories'), [api, refresh]);
  const catalog = useLoad(
    () =>
      catalogOpen
        ? api.request<
            {
              name: string;
              guid: string;
              description: string;
              versions: { version: string; targetAbi?: string; repositoryUrl: string }[];
              Name?: string;
              Guid?: string;
              Versions?: { version: string }[];
            }[]
          >('Packages')
        : Promise.resolve([]),
    [api, catalogOpen],
  );
  return (
    <>
      <h1>Plugins & repositories</h1>
      <Feedback busy={plugins.busy} error={plugins.error} />
      <ActionStatus message={action.message} />
      <div className="admin-card-grid">
        {plugins.value?.map((p) => (
          <section className="panel" key={p.Id + p.Version}>
            <h2>{p.Name}</h2>
            <small>
              {p.Version} · {p.Status}
            </small>
            <p>{p.Description}</p>
            <div className="actions">
              <button onClick={() => setSelected(p.Id)}>Configure</button>
              <button
                disabled={action.busy}
                onClick={() =>
                  void action.run(async () => {
                    await admin.pluginState(p.Id, p.Version, p.Status === 'Disabled');
                    setRefresh((r) => r + 1);
                  })
                }
              >
                {p.Status === 'Disabled' ? 'Enable' : 'Disable'}
              </button>
              <button
                disabled={action.busy}
                onClick={() => {
                  if (confirm('Uninstall ' + p.Name + '?'))
                    void action.run(async () => {
                      await admin.removePlugin(p.Id, p.Version);
                      setRefresh((r) => r + 1);
                    }, 'Plugin uninstalled. A restart may be required.');
                }}
              >
                Uninstall
              </button>
            </div>
          </section>
        ))}
      </div>
      {selected && (
        <>
          <Feedback busy={config.busy} error={config.error} />
          {config.value && (
            <SettingsEditor
              key={selected}
              title="Plugin configuration"
              value={config.value}
              save={(value) =>
                api.request(`Plugins/${selected}/Configuration`, {
                  method: 'POST',
                  body: JSON.stringify(value),
                })
              }
            />
          )}
        </>
      )}
      <Feedback busy={repositories.busy} error={repositories.error} />
      {repositories.value && (
        <JsonCollectionEditor
          title="Plugin repositories"
          value={repositories.value}
          save={(value) =>
            api.request('Repositories', { method: 'POST', body: JSON.stringify(value) })
          }
        />
      )}
      <button onClick={() => setCatalogOpen(!catalogOpen)}>
        {catalogOpen ? 'Hide catalog' : 'Browse available plugins'}
      </button>
      {catalogOpen && (
        <>
          <Feedback busy={catalog.busy} error={catalog.error} />
          <div className="admin-card-grid">
            {catalog.value?.map((p) => (
              <section className="panel" key={p.guid ?? p.Guid}>
                <h3>{p.name ?? p.Name}</h3>
                <p>{p.description}</p>
                <button
                  disabled={action.busy}
                  onClick={() => {
                    const name = p.name ?? p.Name ?? '';
                    if (confirm('Install ' + name + ' from the configured repository?'))
                      void action.run(
                        () => admin.installPlugin(name, p.guid ?? p.Guid ?? ''),
                        'Plugin installation requested.',
                      );
                  }}
                >
                  Install compatible version
                </button>
              </section>
            ))}
          </div>
        </>
      )}
    </>
  );
}
