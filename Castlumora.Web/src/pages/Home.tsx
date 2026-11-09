import { Link } from 'react-router-dom';
import { Radio } from 'lucide-react';
import { deviceClass, orderedRows } from '../config/schema';
import { useApp, useLoad } from '../state';
import { Feedback, Hero, MediaRow } from '../components/Media';
export default function Home() {
  const { api, layout, preferences } = useApp();
  const result = useLoad(async () => {
    const definitions = orderedRows(layout, deviceClass());
    const rows = await Promise.allSettled(
      definitions.map(async (definition) => ({
        definition,
        items: await api.row(definition, layout.categories, preferences),
      })),
    );
    const seen = new Set<string>();
    const good = rows.flatMap((r) => {
      if (r.status !== 'fulfilled') return [];
      const signature = r.value.items.map((i) => i.Id).join(',');
      if (signature && seen.has(signature)) return [];
      if (signature) seen.add(signature);
      return [r.value];
    });
    let featured = good.find((r) => r.items.length)?.items[0];
    for (const banner of layout.featured) {
      try {
        featured = await api.item(banner.itemId);
        break;
      } catch {
        /* inaccessible featured media is never shown */
      }
    }
    return { rows: good, featured, failed: rows.filter((r) => r.status === 'rejected').length };
  }, [api, layout, preferences]);
  return (
    <>
      <Feedback busy={result.busy} error={result.error} />
      {result.value && (
        <>
          {result.value.featured ? (
            <Hero item={result.value.featured} />
          ) : (
            <section className="empty">
              <h1>Your stories start here</h1>
              <p>No media is available to this profile yet.</p>
              <Link to="/browse" className="button primary">
                Browse libraries
              </Link>
            </section>
          )}
          <div className="home-content">
            <div className="home-intro">
              <span className="eyebrow">MAKE AN EVENING OF IT</span>
              <Link to="/live" className="live-link">
                <Radio size={16} />
                Live TV & guide
              </Link>
            </div>
            {result.value.failed > 0 && (
              <p role="status" className="notice">
                Some rows could not load. Try refreshing or check this profile’s permissions.
              </p>
            )}
            {layout.categories.length > 0 && (
              <section>
                <h2>Find your kind of story</h2>
                <div className="category-track">
                  {layout.categories.map((c) => (
                    <Link
                      to={'/browse?category=' + c.id}
                      key={c.id}
                      style={{ borderColor: c.color }}
                    >
                      {c.artworkItemId && (
                        <img
                          src={
                            api.url(`Items/${c.artworkItemId}/Images/Primary`, { maxWidth: 400 })
                              .href
                          }
                          alt=""
                        />
                      )}
                      <span>
                        {c.icon ?? '✦'} {c.title}
                      </span>
                    </Link>
                  ))}
                </div>
              </section>
            )}
            {result.value.rows.map((row) => (
              <MediaRow
                key={row.definition.id}
                title={row.definition.title}
                items={row.items}
                resume={row.definition.kind === 'resume'}
              />
            ))}
          </div>
        </>
      )}
    </>
  );
}
