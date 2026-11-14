import { useState } from 'react';
import { useApp, useLoad } from '../state';
import { Feedback } from '../components/Media';
import { ActionStatus, useAction } from '../admin/Controls';

type Defaults = {
  canEdit: boolean;
  audioLanguage: string | null;
  subtitleLanguage: string | null;
  useDefaultAudioTrack: boolean;
  subtitleMode: string;
};

function PreferencesForm({ initial }: { initial: Defaults }) {
  const { api } = useApp();
  const [draft, setDraft] = useState(initial);
  const action = useAction();
  return (
    <form
      className="panel settings-fields"
      onSubmit={(event) => {
        event.preventDefault();
        void action.run(async () => {
          const saved = await api.request<Defaults>('Castlumora/Profiles/MediaPreferences', {
            method: 'PUT',
            body: JSON.stringify({
              audioLanguage: draft.audioLanguage,
              subtitleLanguage: draft.subtitleLanguage,
              useDefaultAudioTrack: draft.useDefaultAudioTrack,
              subtitleMode: draft.subtitleMode,
            }),
          });
          setDraft(saved);
        }, 'Media preferences saved for this profile.');
      }}
    >
      <label>
        Preferred audio language
        <input
          value={draft.audioLanguage ?? ''}
          maxLength={35}
          autoCapitalize="none"
          placeholder="eng, spa, en-US, or blank"
          pattern="([A-Za-z]{2,3}(-[A-Za-z0-9]{2,8}){0,2}|OriginalLanguage)?"
          onChange={(event) => setDraft({ ...draft, audioLanguage: event.target.value })}
        />
      </label>
      <p>Use an ISO language code, blank for automatic, or OriginalLanguage for original audio.</p>
      <label>
        <input
          type="checkbox"
          checked={draft.useDefaultAudioTrack}
          onChange={(event) => setDraft({ ...draft, useDefaultAudioTrack: event.target.checked })}
        />
        Use source default audio track
      </label>
      <p>Turn this off to prefer your language when a matching audio track is available.</p>
      <label>
        Preferred subtitle language
        <input
          value={draft.subtitleLanguage ?? ''}
          maxLength={35}
          autoCapitalize="none"
          placeholder="eng, spa, en-US, or blank"
          pattern="([A-Za-z]{2,3}(-[A-Za-z0-9]{2,8}){0,2})?"
          onChange={(event) => setDraft({ ...draft, subtitleLanguage: event.target.value })}
        />
      </label>
      <label>
        Subtitle behavior
        <select
          value={draft.subtitleMode}
          onChange={(event) => setDraft({ ...draft, subtitleMode: event.target.value })}
        >
          <option value="Default">Server default</option>
          <option value="Always">Always when available</option>
          <option value="Smart">When audio is in another language</option>
          <option value="OnlyForced">Forced subtitles only</option>
          <option value="None">Off</option>
        </select>
      </label>
      <button disabled={action.busy}>Save media preferences</button>
      <ActionStatus message={action.message} />
    </form>
  );
}

export default function MediaPreferences() {
  const { api } = useApp();
  const defaults = useLoad(
    () => api.request<Defaults>('Castlumora/Profiles/MediaPreferences'),
    [api],
  );
  return (
    <section>
      <h2>Media preferences</h2>
      <p>
        These defaults apply to this viewing identity when matching tracks are available. They do
        not change account access or parental permissions.
      </p>
      <Feedback busy={defaults.busy} error={defaults.error} />
      {defaults.value &&
        (defaults.value.canEdit ? (
          <PreferencesForm initial={defaults.value} />
        ) : (
          <p>Media preferences are controlled by the server administrator.</p>
        ))}
    </section>
  );
}
