import { useEffect, useRef, useState } from 'react';
import { useApp, useLoad } from '../state';
import { ApiError } from '../api/client';
import { Feedback } from '../components/Media';
import { useAction, ActionStatus } from '../admin/Controls';
import { skipperApi } from './api';
import type { SkipMode } from './api';
export function SkipControls({
  id,
  positionTicks,
  onSeek,
}: {
  id: string;
  positionTicks: number;
  onSeek: (seconds: number) => void;
}) {
  const { api } = useApp();
  const [refresh, setRefresh] = useState(0);
  const action = useAction();
  const lastAutomatic = useRef('');
  const [chosenMode, setChosenMode] = useState<SkipMode>();
  const result = useLoad(async () => {
    const skipper = skipperApi(api);
    try {
      const [settings, profile, segments] = await Promise.all([
        skipper.settings(),
        skipper.profile(),
        skipper.segments(id),
      ]);
      return { settings, profile, segments: segments.Items };
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) return undefined;
      throw error;
    }
  }, [api, id, refresh]);
  const preference = chosenMode ?? result.value?.profile.mode;
  const mode = preference === 'default' ? result.value?.settings.defaultMode : preference;
  const active = result.value?.segments.find(
    (s) => positionTicks >= s.StartTicks && positionTicks < s.EndTicks,
  );
  const automaticKey = active ? `${active.Type}:${active.StartTicks}:${active.EndTicks}` : '';
  useEffect(() => {
    if (!active) lastAutomatic.current = '';
    if (mode === 'automatic' && active && lastAutomatic.current !== automaticKey) {
      lastAutomatic.current = automaticKey;
      onSeek(active.EndTicks / 10000000);
    }
  }, [mode, active, automaticKey, onSeek]);
  if (!result.value) return result.error ? <Feedback error={result.error} /> : null;
  return (
    <div className="skip-controls">
      {mode !== 'disabled' && active && (
        <button className="primary" onClick={() => onSeek(active.EndTicks / 10000000)}>
          Skip {active.Type === 'Intro' ? 'Intro' : 'Outro'}
        </button>
      )}
      <label>
        Episode skipping
        <select
          disabled={action.busy}
          value={preference}
          onChange={(e) => {
            const mode = e.target.value as SkipMode;
            setChosenMode(mode);
            void action.run(async () => {
              try {
                await skipperApi(api).saveProfile(mode);
                setRefresh((v) => v + 1);
              } catch (error) {
                setChosenMode(undefined);
                throw error;
              }
            }, 'Skip preference saved for this profile.');
          }}
        >
          <option value="default">Server default ({result.value.settings.defaultMode})</option>
          <option value="manual">Show skip button</option>
          <option value="automatic">Skip automatically</option>
          <option value="disabled">Disabled</option>
        </select>
      </label>
      <ActionStatus message={action.message} />
    </div>
  );
}
