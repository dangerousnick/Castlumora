import { useEffect, useState } from 'react';
import type { ReactNode } from 'react';
import type { ObjectData } from './api';
import { errorText } from '../state';
export type Field = {
  key: string;
  label: string;
  type?: 'text' | 'number' | 'boolean';
  options?: string[];
};
export function useAction() {
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  async function run(action: () => Promise<unknown>, success = 'Saved on the server.') {
    setBusy(true);
    setMessage('');
    try {
      await action();
      setMessage(success);
    } catch (e) {
      setMessage(errorText(e));
    } finally {
      setBusy(false);
    }
  }
  return { busy, message, run };
}
export function ActionStatus({ message }: { message: string }) {
  return message ? (
    <p role="status" className="notice">
      {message}
    </p>
  ) : null;
}
export function SettingsEditor({
  title,
  value,
  fields = [],
  save,
  children,
}: {
  title: string;
  value: ObjectData;
  fields?: Field[];
  save: (value: ObjectData) => Promise<unknown>;
  children?:
    ReactNode | ((form: ObjectData, update: (key: string, value: unknown) => void) => ReactNode);
}) {
  const [form, setForm] = useState(() => structuredClone(value));
  const [json, setJson] = useState(JSON.stringify(value, null, 2));
  const action = useAction();
  useEffect(() => {
    setForm(structuredClone(value));
    setJson(JSON.stringify(value, null, 2));
  }, [value]);
  function update(key: string, v: unknown) {
    const next = { ...form, [key]: v };
    setForm(next);
    setJson(JSON.stringify(next, null, 2));
  }
  return (
    <section className="panel">
      <h2>{title}</h2>
      <div className="settings-fields">
        {fields.map((field) => (
          <label key={field.key}>
            {field.label}
            {field.type === 'boolean' ? (
              <input
                type="checkbox"
                checked={Boolean(form[field.key])}
                onChange={(e) => update(field.key, e.target.checked)}
              />
            ) : field.options ? (
              <select
                value={String(form[field.key] ?? '')}
                onChange={(e) => update(field.key, e.target.value)}
              >
                {field.options.map((v) => (
                  <option key={v} value={v}>
                    {v || 'None / default'}
                  </option>
                ))}
              </select>
            ) : (
              <input
                type={field.type ?? 'text'}
                value={String(form[field.key] ?? '')}
                onChange={(e) =>
                  update(
                    field.key,
                    field.type === 'number'
                      ? e.target.value === ''
                        ? null
                        : Number(e.target.value)
                      : e.target.value,
                  )
                }
              />
            )}
          </label>
        ))}
      </div>
      {typeof children === 'function' ? children(form, update) : children}
      <div className="actions">
        <button
          className="primary"
          disabled={action.busy}
          onClick={() => void action.run(() => save(form))}
        >
          Save {title.toLowerCase()}
        </button>
      </div>
      <ActionStatus message={action.message} />
      <details className="advanced-settings">
        <summary>Advanced settings</summary>
        <p>
          All fields returned by the server are preserved. The server validates changes before
          saving.
        </p>
        <textarea
          aria-label={title + ' advanced settings'}
          rows={16}
          value={json}
          onChange={(e) => setJson(e.target.value)}
        />
        <button
          disabled={action.busy}
          onClick={() =>
            void action.run(async () => {
              const parsed: unknown = JSON.parse(json);
              if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed))
                throw new Error('Settings must be a JSON object.');
              await save(parsed as ObjectData);
              setForm(parsed as ObjectData);
            })
          }
        >
          Save advanced settings
        </button>
      </details>
    </section>
  );
}
export function JsonCollectionEditor({
  title,
  value,
  save,
}: {
  title: string;
  value: unknown[];
  save: (value: unknown[]) => Promise<unknown>;
}) {
  const [json, setJson] = useState(JSON.stringify(value, null, 2));
  const action = useAction();
  return (
    <section className="panel">
      <h2>{title}</h2>
      <textarea
        aria-label={title}
        rows={10}
        value={json}
        onChange={(e) => setJson(e.target.value)}
      />
      <button
        disabled={action.busy}
        onClick={() =>
          void action.run(async () => {
            const parsed: unknown = JSON.parse(json);
            if (!Array.isArray(parsed)) throw new Error('Expected a list.');
            await save(parsed);
          })
        }
      >
        Save {title.toLowerCase()}
      </button>
      <ActionStatus message={action.message} />
    </section>
  );
}
