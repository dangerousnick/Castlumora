import { useMemo, useState } from 'react';
import { useApp, useLoad } from '../state';
import { Feedback } from '../components/Media';
import { adminApi } from './api';
import { ActionStatus, SettingsEditor, useAction } from './Controls';
export default function LiveSetup() {
  const { api } = useApp();
  const admin = useMemo(() => adminApi(api), [api]);
  const [refresh, setRefresh] = useState(0);
  const [tunerName, setTunerName] = useState('');
  const [url, setUrl] = useState('');
  const [tunerType, setTunerType] = useState('m3u');
  const [xmltv, setXmltv] = useState('');
  const action = useAction();
  const config = useLoad(() => admin.config('livetv'), [admin, refresh]);
  const types = useLoad(
    () => api.request<{ Name: string; Id: string }[]>('LiveTv/TunerHosts/Types'),
    [api],
  );
  const mapping = useLoad(
    () => api.request<Record<string, unknown>>('LiveTv/ChannelMappingOptions'),
    [api, refresh],
  );
  const tuners = (config.value?.TunerHosts ?? []) as Record<string, unknown>[];
  const listings = (config.value?.ListingProviders ?? []) as Record<string, unknown>[];
  return (
    <>
      <h1>Live TV & DVR setup</h1>
      <Feedback busy={config.busy} error={config.error} />
      <ActionStatus message={action.message} />
      <div className="admin-card-grid">
        {tuners.map((t) => (
          <section className="panel" key={String(t.Id)}>
            <h2>{String(t.FriendlyName ?? t.Type)}</h2>
            <p>{String(t.Url)}</p>
            <button
              disabled={action.busy}
              onClick={() => {
                if (confirm('Remove this tuner?'))
                  void action.run(async () => {
                    await api.request(
                      'LiveTv/TunerHosts',
                      { method: 'DELETE' },
                      { id: String(t.Id) },
                    );
                    setRefresh((r) => r + 1);
                  });
              }}
            >
              Remove tuner
            </button>
            <SettingsEditor
              title="Tuner settings"
              value={t}
              fields={[
                { key: 'FriendlyName', label: 'Name' },
                { key: 'Url', label: 'Source URL/path' },
                { key: 'TunerCount', label: 'Concurrent tuner streams', type: 'number' },
                { key: 'AllowStreamSharing', label: 'Share compatible streams', type: 'boolean' },
                { key: 'UserAgent', label: 'User agent' },
              ]}
              save={(value) => admin.tuner(value)}
            />
          </section>
        ))}
      </div>
      <section className="panel">
        <h2>Add tuner / IPTV playlist</h2>
        <form
          className="settings-fields"
          onSubmit={(e) => {
            e.preventDefault();
            void action.run(async () => {
              await admin.tuner({
                FriendlyName: tunerName,
                Url: url,
                Type: tunerType,
                AllowStreamSharing: true,
                AllowHWTranscoding: true,
                TunerCount: 0,
              });
              setRefresh((r) => r + 1);
              setUrl('');
            }, 'Tuner saved. Refresh the guide to import channels.');
          }}
        >
          <label>
            Name
            <input required value={tunerName} onChange={(e) => setTunerName(e.target.value)} />
          </label>
          <label>
            Type
            <select value={tunerType} onChange={(e) => setTunerType(e.target.value)}>
              <option value="m3u">M3U / IPTV</option>
              {types.value
                ?.filter((t) => t.Id !== 'm3u')
                .map((t) => (
                  <option key={t.Id} value={t.Id}>
                    {t.Name}
                  </option>
                ))}
            </select>
          </label>
          <label>
            URL or server playlist path
            <input required value={url} onChange={(e) => setUrl(e.target.value)} />
          </label>
          <button disabled={action.busy}>Add tuner</button>
        </form>
      </section>
      <section className="panel">
        <h2>XMLTV guide sources</h2>
        {listings.map((l) => (
          <div className="editor-row" key={String(l.Id)}>
            <span>
              {String(l.Type)} · {String(l.Path ?? l.Username ?? '')}
            </span>
            <button
              onClick={() => {
                if (confirm('Remove guide source?'))
                  void action.run(async () => {
                    await api.request(
                      'LiveTv/ListingProviders',
                      { method: 'DELETE' },
                      { id: String(l.Id) },
                    );
                    setRefresh((r) => r + 1);
                  });
              }}
            >
              Remove
            </button>
          </div>
        ))}
        <form
          className="filters"
          onSubmit={(e) => {
            e.preventDefault();
            void action.run(async () => {
              await admin.listing({ Type: 'xmltv', Path: xmltv, EnableAllTuners: true });
              setRefresh((r) => r + 1);
              setXmltv('');
            }, 'Guide source saved.');
          }}
        >
          <label>
            XMLTV URL or path
            <input required value={xmltv} onChange={(e) => setXmltv(e.target.value)} />
          </label>
          <button disabled={action.busy}>Add XMLTV</button>
        </form>
        <button
          disabled={action.busy}
          onClick={() =>
            void action.run(async () => {
              const tasks = await admin.tasks();
              const guide = tasks.find((t) => /guide/i.test(t.Name));
              if (!guide) throw new Error('Guide refresh task is unavailable.');
              await admin.runTask(guide.Id);
            }, 'Guide refresh queued.')
          }
        >
          Refresh guide
        </button>
      </section>
      {config.value && (
        <SettingsEditor
          title="DVR defaults"
          value={config.value}
          fields={[
            { key: 'RecordingPath', label: 'Recording folder' },
            { key: 'MovieRecordingPath', label: 'Movie recording folder' },
            { key: 'SeriesRecordingPath', label: 'Series recording folder' },
            { key: 'PrePaddingSeconds', label: 'Pre-padding seconds', type: 'number' },
            { key: 'PostPaddingSeconds', label: 'Post-padding seconds', type: 'number' },
            { key: 'SaveRecordingNFO', label: 'Save recording metadata', type: 'boolean' },
          ]}
          save={(value) => admin.saveConfig(value, 'livetv')}
        />
      )}
      <details className="panel">
        <summary>Channel mapping</summary>
        <Feedback busy={mapping.busy} error={mapping.error} />
        {mapping.value && (
          <>
            <p>Guide provider and tuner channel IDs are available below.</p>
            <pre className="server-log">{JSON.stringify(mapping.value, null, 2)}</pre>
            <ChannelMapping />
          </>
        )}
      </details>
    </>
  );
}
function ChannelMapping() {
  const { api } = useApp();
  const [provider, setProvider] = useState('');
  const [channel, setChannel] = useState('');
  const [guide, setGuide] = useState('');
  const action = useAction();
  return (
    <form
      className="settings-fields"
      onSubmit={(e) => {
        e.preventDefault();
        void action.run(() =>
          api.request('LiveTv/ChannelMappings', {
            method: 'POST',
            body: JSON.stringify({
              ProviderId: provider,
              TunerChannelId: channel,
              ProviderChannelId: guide,
            }),
          }),
        );
      }}
    >
      <label>
        Guide provider ID
        <input required value={provider} onChange={(e) => setProvider(e.target.value)} />
      </label>
      <label>
        Tuner channel ID
        <input required value={channel} onChange={(e) => setChannel(e.target.value)} />
      </label>
      <label>
        Guide channel ID
        <input required value={guide} onChange={(e) => setGuide(e.target.value)} />
      </label>
      <button disabled={action.busy}>Save channel mapping</button>
      <ActionStatus message={action.message} />
    </form>
  );
}
