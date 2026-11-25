import { useEffect, useRef, useState } from 'react';
import ePub from 'epubjs';
import type Rendition from 'epubjs/types/rendition';
import type { Location } from 'epubjs/types/rendition';
import { useApp } from '../state';
import { Feedback } from '../components/Media';
import { readerApi } from './api';
import { prepareEpub } from './epubSafety';
export default function EpubReader({
  id,
  initial,
  theme,
  fontSize,
  changed,
}: {
  id: string;
  initial: string;
  theme: string;
  fontSize: number;
  changed: (location: string) => void;
}) {
  const { api } = useApp();
  const element = useRef<HTMLDivElement>(null);
  const rendition = useRef<Rendition | undefined>(undefined);
  const onChange = useRef(changed);
  useEffect(() => {
    onChange.current = changed;
  }, [changed]);
  const [busy, setBusy] = useState(true);
  const [error, setError] = useState('');
  const [toc, setToc] = useState<{ label: string; href: string }[]>([]);
  const [progress, setProgress] = useState('');
  const [ready, setReady] = useState(0);
  useEffect(() => {
    let cancelled = false;
    let book: ReturnType<typeof ePub> | undefined;
    setBusy(true);
    setError('');
    void (async () => {
      const data = await prepareEpub(await readerApi(api).epub(id));
      if (cancelled) return;
      book = ePub(data);
      await book.ready;
      if (cancelled) return;
      const view = book.renderTo(element.current!, {
        width: '100%',
        height: Math.max(440, window.innerHeight - 280),
        spread: 'none',
        flow: 'paginated',
        allowScriptedContent: false,
      });
      rendition.current = view;
      view.on('relocated', (location: Location) => {
        if (!cancelled) {
          onChange.current(location.start.cfi);
          setProgress(`${Math.round((location.start.percentage ?? 0) * 100)}%`);
        }
      });
      const navigation = await book.loaded.navigation;
      const entries: { label: string; href: string }[] = [];
      function collect(items: typeof navigation.toc) {
        for (const item of items) {
          if (!/^(?:[a-z][a-z0-9+.-]*:|\/\/)/i.test(item.href))
            entries.push({ label: item.label, href: item.href });
          if (item.subitems?.length) collect(item.subitems);
        }
      }
      collect(navigation.toc);
      setToc(entries);
      await view.display(initial.startsWith('epubcfi(') ? initial : undefined);
      if (!cancelled) {
        setBusy(false);
        setReady((value) => value + 1);
      }
    })().catch(() => {
      if (!cancelled) {
        setBusy(false);
        setError(
          'This EPUB could not be opened safely. It may be unsupported, damaged, encrypted or exceed reader resource limits.',
        );
      }
    });
    return () => {
      cancelled = true;
      rendition.current?.destroy();
      rendition.current = undefined;
      book?.destroy();
    };
    // Position is used only when opening this book; relocation persists it without reopening the renderer.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [api, id]);
  useEffect(() => {
    const colors =
      theme === 'light'
        ? ['#ffffff', '#17121e']
        : theme === 'sepia'
          ? ['#f4ebd5', '#3c3225']
          : ['#17121e', '#f4edff'];
    rendition.current?.themes.default({
      body: { color: colors[1], background: colors[0] },
      a: { color: theme === 'light' ? '#6430a0' : '#c7a0ff' },
    });
    rendition.current?.themes.fontSize(fontSize + 'px');
  }, [theme, fontSize, ready]);
  return (
    <div className="epub-reader">
      <div className="reader-toolbar">
        <button disabled={busy} onClick={() => void rendition.current?.prev()}>
          Previous page
        </button>
        <label>
          Book chapter
          <select defaultValue="" onChange={(e) => void rendition.current?.display(e.target.value)}>
            <option value="" disabled>
              Choose chapter
            </option>
            {toc.map((entry, index) => (
              <option key={index} value={entry.href}>
                {entry.label}
              </option>
            ))}
          </select>
        </label>
        <button disabled={busy} onClick={() => void rendition.current?.next()}>
          Next page
        </button>
        <span>{progress}</span>
      </div>
      <Feedback busy={busy} error={error} />
      <div ref={element} className="epub-canvas" />
    </div>
  );
}
