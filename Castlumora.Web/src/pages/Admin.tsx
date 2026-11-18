import { useState } from 'react';
import { Link } from 'react-router-dom';
import { ArrowDown, ArrowUp, Plus, Save, Upload, Trash2 } from 'lucide-react';
import type { Layout } from '../config/schema';
import { layoutSchema, orderedRows } from '../config/schema';
import type { User } from '../api/types';
import { errorText, useApp, useLoad } from '../state';
import { Feedback } from '../components/Media';
import { PresentationHistory } from '../components/PresentationHistory';
import { MediaPicker } from '../components/MediaPicker';
function move<T>(items: T[], index: number, delta: number) {
  const updated = [...items];
  const target = index + delta;
  if (target < 0 || target >= items.length) return updated;
  [updated[index], updated[target]] = [updated[target], updated[index]];
  return updated;
}
function Reorder({
  index,
  length,
  onMove,
}: {
  index: number;
  length: number;
  onMove: (delta: number) => void;
}) {
  return (
    <div className="reorder">
      <button aria-label="Move up" disabled={index === 0} onClick={() => onMove(-1)}>
        <ArrowUp size={15} />
      </button>
      <button aria-label="Move down" disabled={index === length - 1} onClick={() => onMove(1)}>
        <ArrowDown size={15} />
      </button>
    </div>
  );
}
function LayoutEditor({ initial, users }: { initial: Layout; users: User[] }) {
  const { api, refreshLayout } = useApp();
  const [draft, setDraft] = useState(initial);
  const [device, setDevice] = useState('desktop');
  const [message, setMessage] = useState('');
  const [busy, setBusy] = useState(false);
  const [dirty, setDirty] = useState(false);
  const [advanced, setAdvanced] = useState(JSON.stringify(initial, null, 2));
  function change(next: Layout) {
    setDraft(next);
    setAdvanced(JSON.stringify(next, null, 2));
    setDirty(true);
    setMessage('');
  }
  async function save(publish: boolean) {
    setBusy(true);
    setMessage('');
    try {
      const parsed = layoutSchema.parse(draft);
      const updated = publish ? await api.publish(parsed) : await api.saveDraft(parsed);
      setDraft(updated);
      setAdvanced(JSON.stringify(updated, null, 2));
      setDirty(false);
      setMessage(
        publish ? 'Layout published to compatible clients.' : 'Draft saved on the server.',
      );
      if (publish) await refreshLayout();
    } catch (e) {
      setMessage(errorText(e));
    } finally {
      setBusy(false);
    }
  }
  const rows = orderedRows(draft, device);
  return (
    <div className="editor">
      <div className="editor-toolbar">
        <div>
          <span className="eyebrow">SERVER PRESENTATION · SCHEMA V1</span>
          <h2>Make it feel like home</h2>
          <p>Draft revision {draft.revision}. Saving and publishing are separate steps.</p>
        </div>
        <div className="actions">
          <button disabled={busy} onClick={() => void save(false)}>
            <Save size={16} />
            Save draft
          </button>
          <button disabled={busy || dirty} onClick={() => void save(true)} className="primary">
            <Upload size={16} />
            Publish
          </button>
        </div>
      </div>
      {message && (
        <p role="status" className="notice">
          {message}
        </p>
      )}
      {dirty && <p className="muted">Save the draft before publishing.</p>}
      <PresentationHistory
        draft={draft}
        dirty={dirty}
        onRestore={(config) => {
          setDraft(config);
          setAdvanced(JSON.stringify(config, null, 2));
          setDirty(false);
          setMessage('Revision restored to draft. Review and publish when ready.');
        }}
      />
      <section className="panel">
        <h3>Featured banners</h3>
        <p>
          Choose real titles and their server artwork. Published order controls the featured
          content.
        </p>
        {draft.featured.map((f, index) => (
          <div className="editor-row" key={index}>
            <span>{f.itemId}</span>
            <Reorder
              index={index}
              length={draft.featured.length}
              onMove={(delta) => change({ ...draft, featured: move(draft.featured, index, delta) })}
            />
            <button
              onClick={() =>
                change({ ...draft, featured: draft.featured.filter((_, i) => i !== index) })
              }
            >
              Remove
            </button>
          </div>
        ))}
        <MediaPicker
          label="Add featured title"
          onChoose={(item) =>
            change({ ...draft, featured: [...draft.featured, { itemId: item.Id }] })
          }
        />
      </section>
      <div className="editor-columns">
        <div>
          <section className="panel">
            <h3>Navigation</h3>
            {draft.navigation.map((nav, index) => (
              <div className="editor-row" key={nav.id}>
                <input
                  aria-label={nav.id + ' navigation label'}
                  value={nav.label}
                  onChange={(e) =>
                    change({
                      ...draft,
                      navigation: draft.navigation.map((v, i) =>
                        i === index ? { ...v, label: e.target.value } : v,
                      ),
                    })
                  }
                />
                <Reorder
                  index={index}
                  length={draft.navigation.length}
                  onMove={(d) => change({ ...draft, navigation: move(draft.navigation, index, d) })}
                />
                <button
                  aria-label={'Remove ' + nav.label}
                  onClick={() =>
                    change({ ...draft, navigation: draft.navigation.filter((_, i) => i !== index) })
                  }
                >
                  <Trash2 size={15} />
                </button>
              </div>
            ))}
            <select
              aria-label="Add navigation item"
              value=""
              onChange={(e) => {
                if (e.target.value)
                  change({
                    ...draft,
                    navigation: [
                      ...draft.navigation,
                      {
                        id: e.target.value as Layout['navigation'][number]['id'],
                        label: {
                          home: 'Home',
                          browse: 'Browse',
                          list: 'My List',
                          search: 'Search',
                        }[e.target.value as 'home'],
                      },
                    ],
                  });
              }}
            >
              <option value="">Add navigation item…</option>
              {['home', 'browse', 'list', 'search']
                .filter((id) => !draft.navigation.some((n) => n.id === id))
                .map((id) => (
                  <option key={id}>{id}</option>
                ))}
            </select>
          </section>
          <section className="panel">
            <h3>Home rows</h3>
            {draft.rows.map((row, index) => (
              <div className="editor-row" key={row.id}>
                <input
                  aria-label={'Title for ' + row.id}
                  value={row.title}
                  onChange={(e) =>
                    change({
                      ...draft,
                      rows: draft.rows.map((v, i) =>
                        i === index ? { ...v, title: e.target.value } : v,
                      ),
                    })
                  }
                />
                <span className="muted">{row.kind}</span>
                <Reorder
                  index={index}
                  length={draft.rows.length}
                  onMove={(d) => change({ ...draft, rows: move(draft.rows, index, d) })}
                />
                <button
                  aria-label={'Delete ' + row.title}
                  onClick={() => {
                    const next = {
                      ...draft,
                      rows: draft.rows.filter((_, i) => i !== index),
                      devices: structuredClone(draft.devices),
                    };
                    for (const v of Object.values(next.devices))
                      if (v?.rowIds) v.rowIds = v.rowIds.filter((id) => id !== row.id);
                    change(next);
                  }}
                >
                  <Trash2 size={15} />
                </button>
              </div>
            ))}
            <button
              onClick={() =>
                change({
                  ...draft,
                  rows: [
                    ...draft.rows,
                    { id: crypto.randomUUID(), title: 'Recently Added', kind: 'recent' },
                  ],
                })
              }
            >
              <Plus size={15} />
              Add row
            </button>
          </section>
          <section className="panel">
            <h3>Theme</h3>
            <div className="filters">
              <label>
                Accent
                <input
                  type="color"
                  value={draft.theme.accent}
                  onChange={(e) =>
                    change({ ...draft, theme: { ...draft.theme, accent: e.target.value } })
                  }
                />
              </label>
              <label>
                Background
                <input
                  type="color"
                  value={draft.theme.background}
                  onChange={(e) =>
                    change({ ...draft, theme: { ...draft.theme, background: e.target.value } })
                  }
                />
              </label>
            </div>
          </section>
        </div>
        <div>
          <section className="panel preview-panel">
            <div className="section-heading">
              <h3>Layout preview</h3>
              <select
                aria-label="Preview device"
                value={device}
                onChange={(e) => setDevice(e.target.value)}
              >
                {['desktop', 'tablet', 'mobile', 'tv'].map((d) => (
                  <option key={d}>{d}</option>
                ))}
              </select>
            </div>
            <div
              className={'layout-preview ' + device}
              style={{ background: draft.theme.background, borderColor: draft.theme.accent }}
            >
              <nav>
                {draft.navigation.map((n) => (
                  <span key={n.id}>{n.label}</span>
                ))}
              </nav>
              <div className="preview-banner">
                <img src="./castle.svg" alt="" />
                <span>Featured artwork</span>
              </div>
              {rows.map((r) => (
                <div key={r.id}>
                  <strong>{r.title}</strong>
                  <div className="preview-posters">
                    {[1, 2, 3, 4, 5].map((n) => (
                      <span key={n} style={{ borderColor: draft.theme.accent }} />
                    ))}
                  </div>
                </div>
              ))}
            </div>
            <small>
              Layout preview only. Rectangles represent media, not sample server content.
            </small>
          </section>
          <section className="panel">
            <h3>Device presentation</h3>
            <label>
              Poster width
              <input
                type="number"
                min={100}
                max={320}
                value={draft.devices[device as keyof Layout['devices']]?.posterWidth ?? 180}
                onChange={(e) =>
                  change({
                    ...draft,
                    devices: {
                      ...draft.devices,
                      [device]: {
                        ...draft.devices[device as keyof Layout['devices']],
                        posterWidth: Number(e.target.value),
                      },
                    },
                  })
                }
              />
            </label>
            <label>
              Safe zone (%)
              <input
                type="number"
                min={0}
                max={10}
                value={draft.devices[device as keyof Layout['devices']]?.safeZone ?? 0}
                onChange={(e) =>
                  change({
                    ...draft,
                    devices: {
                      ...draft.devices,
                      [device]: {
                        ...draft.devices[device as keyof Layout['devices']],
                        safeZone: Number(e.target.value),
                      },
                    },
                  })
                }
              />
            </label>
          </section>
        </div>
      </div>
      <section className="panel">
        <div className="section-heading">
          <h3>Categories</h3>
          <button
            onClick={() => {
              const id = crypto.randomUUID();
              change({
                ...draft,
                categories: [
                  ...draft.categories,
                  {
                    id,
                    title: 'New category',
                    icon: '✦',
                    rules: { itemIds: [], folderIds: [], genres: [], studios: [] },
                  },
                ],
                rows: [
                  ...draft.rows,
                  { id: 'category-' + id, title: 'New category', kind: 'category', categoryId: id },
                ],
              });
            }}
          >
            <Plus size={15} />
            Add category
          </button>
        </div>
        <p>
          Rule groups are combined with OR; exclusions always win. Library permissions are always
          enforced by the server.
        </p>
        {draft.categories.map((c, index) => (
          <div className="category-editor" key={c.id}>
            <div className="editor-row">
              <label>
                Name
                <input
                  value={c.title}
                  onChange={(e) =>
                    change({
                      ...draft,
                      categories: draft.categories.map((v, i) =>
                        i === index ? { ...v, title: e.target.value } : v,
                      ),
                      rows: draft.rows.map((r) =>
                        r.categoryId === c.id ? { ...r, title: e.target.value } : r,
                      ),
                    })
                  }
                />
              </label>
              <label>
                Icon
                <input
                  value={c.icon ?? ''}
                  onChange={(e) =>
                    change({
                      ...draft,
                      categories: draft.categories.map((v, i) =>
                        i === index ? { ...v, icon: e.target.value } : v,
                      ),
                    })
                  }
                />
              </label>
              <Reorder
                index={index}
                length={draft.categories.length}
                onMove={(d) => change({ ...draft, categories: move(draft.categories, index, d) })}
              />
            </div>
            <div className="filters">
              <label>
                Enabled
                <input
                  type="checkbox"
                  checked={c.enabled !== false}
                  onChange={(e) =>
                    change({
                      ...draft,
                      categories: draft.categories.map((v, i) =>
                        i === index ? { ...v, enabled: e.target.checked } : v,
                      ),
                    })
                  }
                />
              </label>
              <label>
                Color
                <input
                  type="color"
                  value={c.color ?? '#7020d6'}
                  onChange={(e) =>
                    change({
                      ...draft,
                      categories: draft.categories.map((v, i) =>
                        i === index ? { ...v, color: e.target.value } : v,
                      ),
                    })
                  }
                />
              </label>
              <label>
                Content types
                <select
                  multiple
                  value={c.rules.contentTypes ?? ['Movie', 'Series']}
                  onChange={(e) =>
                    change({
                      ...draft,
                      categories: draft.categories.map((v, i) =>
                        i === index
                          ? {
                              ...v,
                              rules: {
                                ...v.rules,
                                contentTypes: [...e.target.selectedOptions].map(
                                  (o) => o.value as 'Movie' | 'Series' | 'Episode',
                                ),
                              },
                            }
                          : v,
                      ),
                    })
                  }
                >
                  {['Movie', 'Series', 'Episode'].map((t) => (
                    <option key={t}>{t}</option>
                  ))}
                </select>
              </label>
            </div>
            <MediaPicker
              label="Category artwork"
              onChoose={(item) =>
                change({
                  ...draft,
                  categories: draft.categories.map((v, i) =>
                    i === index ? { ...v, artworkItemId: item.Id } : v,
                  ),
                })
              }
            />
            <div className="rule-fields">
              {(
                ['genres', 'studios', 'folderNames', 'folderIds', 'itemIds', 'excludeIds'] as const
              ).map((key) => (
                <label key={key}>
                  {
                    {
                      genres: 'Genres',
                      studios: 'Studios',
                      folderNames: 'Folder names',
                      folderIds: 'Folder IDs',
                      itemIds: 'Include item IDs',
                      excludeIds: 'Exclude item IDs',
                    }[key]
                  }{' '}
                  (comma separated)
                  <input
                    value={c.rules[key]?.join(', ') ?? ''}
                    onChange={(e) =>
                      change({
                        ...draft,
                        categories: draft.categories.map((v, i) =>
                          i === index
                            ? {
                                ...v,
                                rules: {
                                  ...v.rules,
                                  [key]: e.target.value
                                    .split(',')
                                    .map((s) => s.trim())
                                    .filter(Boolean),
                                },
                              }
                            : v,
                        ),
                      })
                    }
                  />
                </label>
              ))}
              <label>
                Visible profiles (none = all)
                <select
                  multiple
                  value={c.userIds ?? []}
                  onChange={(e) =>
                    change({
                      ...draft,
                      categories: draft.categories.map((v, i) =>
                        i === index
                          ? { ...v, userIds: [...e.target.selectedOptions].map((o) => o.value) }
                          : v,
                      ),
                    })
                  }
                >
                  {users.map((u) => (
                    <option key={u.Id} value={u.Id}>
                      {u.Name}
                    </option>
                  ))}
                </select>
              </label>
            </div>
          </div>
        ))}
      </section>
      <details className="panel">
        <summary>
          Advanced schema editor: banners, artwork, row visibility and device ordering
        </summary>
        <p>
          Use media IDs from details URLs. No HTML or arbitrary script components are supported.
        </p>
        <textarea
          aria-label="Presentation JSON"
          rows={18}
          value={advanced}
          onChange={(e) => setAdvanced(e.target.value)}
        />
        <button
          onClick={() => {
            try {
              change(layoutSchema.parse(JSON.parse(advanced)));
              setMessage('JSON applied to preview. Save the draft to persist it.');
            } catch (e) {
              setMessage(errorText(e));
            }
          }}
        >
          Validate & apply to preview
        </button>
      </details>
    </div>
  );
}
const sections = [
  ['Libraries', 'libraries'],
  ['Metadata Management', 'metadata'],
  ['Imports & Sources', 'imports'],
  ['Live TV & DVR', 'live'],
  ['Playback & Encoding', 'encoding'],
  ['Users & Access', 'users'],
  ['Plugins', 'plugins'],
  ['System & backups', 'system'],
];
export default function Admin() {
  const { api, session } = useApp();
  const result = useLoad(async () => {
    if (!session.user.Policy?.IsAdministrator) throw new Error('Administrator access required.');
    const [draft, users, status, sessions] = await Promise.all([
      api.draft(),
      api.request<User[]>('Users'),
      api.request<{ ServerName: string; Version: string }>('System/Info'),
      api.request<{ Id: string; UserName?: string; Client: string }[]>('Sessions'),
    ]);
    return { draft, users, status, sessions };
  }, [api, session]);
  if (!session.user.Policy?.IsAdministrator)
    return (
      <main className="page">
        <Feedback error="Administrator access required." />
      </main>
    );
  return (
    <div className="page admin">
      <div className="page-heading">
        <div>
          <span className="eyebrow">THE CONTROL ROOM</span>
          <h1>Your castle. Your rules.</h1>
          <p>One presentation, built for every screen.</p>
        </div>
        <a
          className="button secondary"
          href={api.legacy()}
          target="_blank"
          rel="noopener noreferrer"
        >
          Full server administration ↗
        </a>
      </div>
      <Feedback busy={result.busy} error={result.error} />
      {result.value && (
        <>
          <section className="status-grid">
            <article className="panel">
              <small>SERVER</small>
              <h2>{result.value.status.ServerName}</h2>
              <p>Backend {result.value.status.Version}</p>
            </article>
            <article className="panel">
              <small>PROFILES</small>
              <h2>{result.value.users.length}</h2>
              <p>{result.value.users.map((u) => u.Name).join(', ')}</p>
            </article>
            <article className="panel">
              <small>ACTIVE SESSIONS</small>
              <h2>{result.value.sessions.length}</h2>
              <p>{result.value.sessions.map((s) => s.UserName ?? s.Client).join(', ')}</p>
            </article>
          </section>
          <nav className="admin-sections">
            {sections.map(([name, path]) => (
              <Link key={name} to={'/admin/' + path}>
                {name}
              </Link>
            ))}
          </nav>
          <p className="muted">
            The sections above use the existing server APIs. The existing dashboard remains
            available for any unsupported setting or plugin-specific screen.
          </p>
          <LayoutEditor initial={result.value.draft} users={result.value.users} />
        </>
      )}
    </div>
  );
}
