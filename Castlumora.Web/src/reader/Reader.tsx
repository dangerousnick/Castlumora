import { lazy, Suspense, useEffect, useRef, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { errorText, useApp, useLoad } from '../state';
import { Feedback } from '../components/Media';
import { readerApi } from './api';
import type { ReadingState } from './api';
const PdfReader = lazy(() => import('./PdfReader'));
const EpubReader = lazy(() => import('./EpubReader'));
export default function Reader() {
  const { id = '' } = useParams();
  const { api } = useApp();
  const result = useLoad(async () => {
    const item = await api.item(id);
    if (item.Type !== 'Book') throw new Error('This item is not a book.');
    const [state, type] = await Promise.all([readerApi(api).state(id), readerApi(api).probe(id)]);
    return { item, state, type };
  }, [api, id]);
  return (
    <main className="page reader-page">
      <Link to={'/item/' + id}>← Back to book</Link>
      <Feedback busy={result.busy} error={result.error} />
      {result.value && (
        <BookView
          key={id}
          id={id}
          title={result.value.item.Name}
          initial={result.value.state}
          type={result.value.type}
        />
      )}
    </main>
  );
}
function BookView({
  id,
  title,
  initial,
  type,
}: {
  id: string;
  title: string;
  initial: ReadingState;
  type?: string;
}) {
  const { api } = useApp();
  const [preferences, setPreferences] = useState(initial);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const latest = useRef(initial);
  const writes = useRef(Promise.resolve());
  const active = useRef(true);
  useEffect(() => {
    active.current = true;
    return () => {
      active.current = false;
    };
  }, []);
  function persist(value: ReadingState) {
    latest.current = value;
    setPreferences(value);
    if (!value.format) return;
    writes.current = writes.current.then(async () => {
      try {
        await readerApi(api).save(id, value);
        if (active.current) {
          setError('');
          setMessage('Reading position saved for your profile.');
        }
      } catch (error) {
        if (active.current) setError(errorText(error));
      }
    });
  }
  function changed(location: string, format: 'pdf' | 'epub') {
    persist({ ...latest.current, location, format });
  }
  const supported = type === 'application/pdf' || type === 'application/epub+zip';
  return (
    <section className={'reader-shell reader-' + preferences.theme}>
      <div className="page-heading">
        <h1>{title}</h1>
        <div className="filters">
          <label>
            Reader theme
            <select
              value={preferences.theme}
              onChange={(e) => {
                persist({ ...latest.current, theme: e.target.value as ReadingState['theme'] });
              }}
            >
              <option value="dark">Dark</option>
              <option value="light">Light</option>
              <option value="sepia">Sepia</option>
            </select>
          </label>
          {type === 'application/epub+zip' && (
            <label>
              Text size
              <input
                type="number"
                min="10"
                max="40"
                value={preferences.fontSize}
                onChange={(e) => {
                  const fontSize = Number(e.target.value);
                  if (Number.isFinite(fontSize) && fontSize >= 10 && fontSize <= 40)
                    persist({ ...latest.current, fontSize });
                }}
              />
            </label>
          )}
        </div>
      </div>
      <Suspense fallback={<Feedback busy />}>
        {type === 'application/pdf' ? (
          <PdfReader
            id={id}
            initial={initial.location}
            changed={(location) => void changed(location, 'pdf')}
          />
        ) : type === 'application/epub+zip' ? (
          <EpubReader
            id={id}
            initial={initial.location}
            theme={preferences.theme}
            fontSize={preferences.fontSize}
            changed={(location) => void changed(location, 'epub')}
          />
        ) : (
          <p>
            This book format requires an external reader. Use the existing client or an authorized
            download from its details page.
          </p>
        )}
      </Suspense>
      {supported && <p role="status">{message}</p>}
      {error && (
        <p className="error" role="alert">
          {error}
        </p>
      )}
    </section>
  );
}
