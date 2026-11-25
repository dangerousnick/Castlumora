import { sameId } from '../api/client';
import { useApp, useLoad } from '../state';
import { Feedback } from '../components/Media';
import { adminApi } from './api';
import type { ObjectData } from './api';
import { SettingsEditor } from './Controls';
export function PolicyEditor({
  title,
  value,
  save,
  allowAdministrator = true,
}: {
  title: string;
  value: ObjectData;
  save: (value: ObjectData) => Promise<unknown>;
  allowAdministrator?: boolean;
}) {
  const { api } = useApp();
  const libraries = useLoad(() => adminApi(api).libraries(), [api]);
  return (
    <SettingsEditor
      title={title}
      value={value}
      fields={[
        ...(allowAdministrator
          ? [{ key: 'IsAdministrator', label: 'Administrator', type: 'boolean' as const }]
          : []),
        {
          key: 'IsDisabled',
          label: allowAdministrator ? 'Disable account' : 'Require administrator activation',
          type: 'boolean',
        },
        { key: 'EnableMediaPlayback', label: 'Allow playback', type: 'boolean' },
        { key: 'EnableLiveTvAccess', label: 'Live TV access', type: 'boolean' },
        { key: 'EnableAllChannels', label: 'All Live TV channels', type: 'boolean' },
        { key: 'EnableLiveTvManagement', label: 'DVR management', type: 'boolean' },
        { key: 'EnableContentDownloading', label: 'Allow downloads', type: 'boolean' },
        { key: 'EnableVideoPlaybackTranscoding', label: 'Video transcoding', type: 'boolean' },
        { key: 'EnableAudioPlaybackTranscoding', label: 'Audio transcoding', type: 'boolean' },
        { key: 'EnableAllFolders', label: 'All libraries', type: 'boolean' },
        { key: 'MaxParentalRating', label: 'Maximum parental rating score', type: 'number' },
        {
          key: 'MaxActiveSessions',
          label: 'Maximum active sessions (0 = unrestricted)',
          type: 'number',
        },
      ]}
      save={save}
    >
      {(form, update) => {
        const ids = Array.isArray(form.EnabledFolders)
          ? form.EnabledFolders.filter((v): v is string => typeof v === 'string')
          : [];
        return (
          <fieldset>
            <legend>Allowed libraries</legend>
            <Feedback busy={libraries.busy} error={libraries.error} />
            {libraries.value?.map((library) => (
              <label className="library-permission" key={library.ItemId}>
                <input
                  type="checkbox"
                  disabled={Boolean(form.EnableAllFolders)}
                  checked={
                    Boolean(form.EnableAllFolders) || ids.some((id) => sameId(id, library.ItemId))
                  }
                  onChange={(e) =>
                    update(
                      'EnabledFolders',
                      e.target.checked
                        ? [...ids, library.ItemId]
                        : ids.filter((id) => !sameId(id, library.ItemId)),
                    )
                  }
                />
                {library.Name}
              </label>
            ))}
            {!allowAdministrator && (
              <p>
                Self-registration always creates a non-administrator. Choose accessible libraries
                explicitly, or enable all libraries.
              </p>
            )}
          </fieldset>
        );
      }}
    </SettingsEditor>
  );
}
