import { useState } from 'react';
import { useApp, useLoad } from '../state';
import { Feedback } from '../components/Media';
import { MediaPicker } from '../components/MediaPicker';
import { SettingsEditor, ActionStatus, useAction } from './Controls';
import { skipperApi } from '../skipper/api';
import type { ReviewState, SkipMode } from '../skipper/api';
import type { Item } from '../api/types';
export default function Skipper() {
  const { api } = useApp();
  const [item, setItem] = useState<Item>();
  const [refresh, setRefresh] = useState(0);
  const settings = useLoad(() => skipperApi(api).settings(), [api, refresh]);
  const review = useLoad(
    () => (item ? skipperApi(api).review(item.Id) : Promise.resolve(undefined)),
    [api, item, refresh],
  );
  const action = useAction();
  return (
    <div className="page admin">
      <h1>Built-in intro & outro skipper</h1>
      <p>
        Native local analysis compares at least three episodes in the same season. Suggestions never
        become skips until reviewed and approved. Protect post-credit scenes before approving an
        outro.
      </p>
      <Feedback busy={settings.busy || review.busy} error={settings.error || review.error} />
      {settings.value && (
        <SettingsEditor
          title="skipper settings"
          value={settings.value}
          fields={[
            {
              key: 'analysisEnabled',
              label: 'Enable optional local repeated-audio analysis',
              type: 'boolean',
            },
            {
              key: 'defaultMode',
              label: 'Default profile skip behavior',
              options: ['manual', 'automatic', 'disabled'],
            },
          ]}
          save={async (value) => {
            await skipperApi(api).saveSettings(
              value as { analysisEnabled: boolean; defaultMode: SkipMode },
            );
            setRefresh((v) => v + 1);
          }}
        />
      )}
      <p>
        Use the existing Extract Media Segments scheduled task for background processing. Analysis
        is single-threaded and deferred while playback is active; it supports local episode files.
        Each run decodes bounded beginning/end windows from at most four episodes. Existing segment
        providers remain available.
      </p>
      <MediaPicker label="Choose episode to review" types="Episode" onChoose={setItem} />
      {item && (
        <>
          <h2>
            {item.SeriesName ? item.SeriesName + ' — ' : ''}
            {item.Name}
          </h2>
          <button
            disabled={action.busy}
            onClick={() =>
              void action.run(async () => {
                await skipperApi(api).analyze(item.Id);
                setRefresh((v) => v + 1);
              }, 'Analysis complete. Review suggestions before approving.')
            }
          >
            Analyze this episode
          </button>
        </>
      )}
      <ActionStatus message={action.message} />
      {item && review.value && (
        <IntervalEditor
          key={item.Id + '-' + review.value.revision}
          item={item}
          initial={review.value}
          save={async (value) => {
            await skipperApi(api).save(item.Id, value);
            setRefresh((v) => v + 1);
          }}
        />
      )}
    </div>
  );
}
function IntervalEditor({
  item,
  initial,
  save,
}: {
  item: Item;
  initial: ReviewState;
  save: (value: ReviewState) => Promise<void>;
}) {
  const [state, setState] = useState(() => structuredClone(initial));
  const action = useAction();
  const update = (index: number, value: Partial<ReviewState['segments'][number]>) =>
    setState((v) => ({
      ...v,
      segments: v.segments.map((segment, n) => (n === index ? { ...segment, ...value } : segment)),
    }));
  return (
    <section>
      <h2>Central intervals</h2>
      <p>
        Revision {state.revision}. Duration {Math.round((item.RunTimeTicks ?? 0) / 10000000)}{' '}
        seconds. Saving refreshes enabled segment providers; approved intervals are available
        through the shared server MediaSegments API.
      </p>
      <label>
        Protect content from this second (optional)
        <input
          type="number"
          min="0"
          step="0.1"
          value={
            state.protectedFromTicks === null || state.protectedFromTicks === undefined
              ? ''
              : state.protectedFromTicks / 10000000
          }
          onChange={(e) =>
            setState((v) => ({
              ...v,
              protectedFromTicks: e.target.value === '' ? null : Math.round(Number(e.target.value) * 10000000),
            }))
          }
        />
      </label>
      {state.segments.map((segment, index) => (
        <fieldset key={index}>
          <legend>
            {segment.type} interval {index + 1}
          </legend>
          <div className="admin-fields">
            <label>
              Segment type
              <select
                value={segment.type}
                onChange={(e) => update(index, { type: e.target.value as 'Intro' | 'Outro' })}
              >
                <option>Intro</option>
                <option>Outro</option>
              </select>
            </label>
            <label>
              Start (seconds)
              <input
                type="number"
                min="0"
                step="0.1"
                value={segment.startTicks / 10000000}
                onChange={(e) =>
                  update(index, { startTicks: Math.round(Number(e.target.value) * 10000000) })
                }
              />
            </label>
            <label>
              End (seconds)
              <input
                type="number"
                min="0"
                step="0.1"
                value={segment.endTicks / 10000000}
                onChange={(e) =>
                  update(index, { endTicks: Math.round(Number(e.target.value) * 10000000) })
                }
              />
            </label>
            <label>
              <input
                type="checkbox"
                checked={segment.status === 'approved'}
                onChange={(e) =>
                  update(index, { status: e.target.checked ? 'approved' : 'suggested' })
                }
              />{' '}
              Reviewed and approved for skipping
            </label>
          </div>
          <button
            onClick={() =>
              setState((v) => ({ ...v, segments: v.segments.filter((_, n) => n !== index) }))
            }
          >
            Remove interval {index + 1}
          </button>
        </fieldset>
      ))}
      <div className="actions">
        <button
          onClick={() =>
            setState((v) => ({
              ...v,
              segments: [
                ...v.segments,
                { type: 'Intro', startTicks: 0, endTicks: 100000000, status: 'suggested' },
              ],
            }))
          }
        >
          Add manual interval
        </button>
        <button
          className="primary"
          disabled={action.busy}
          onClick={() => void action.run(() => save(state), 'Reviewed intervals saved.')}
        >
          Save reviewed intervals
        </button>
      </div>
      <ActionStatus message={action.message} />
    </section>
  );
}
