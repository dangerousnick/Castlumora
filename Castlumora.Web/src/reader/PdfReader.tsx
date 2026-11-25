import { useEffect, useRef, useState } from 'react';
import * as pdfjs from 'pdfjs-dist';
import type { PDFDocumentProxy, RenderTask } from 'pdfjs-dist';
import workerUrl from 'pdfjs-dist/build/pdf.worker.min.mjs?url';
import { useApp } from '../state';
import { Feedback } from '../components/Media';
pdfjs.GlobalWorkerOptions.workerSrc = workerUrl;
export default function PdfReader({
  id,
  initial,
  changed,
}: {
  id: string;
  initial: string;
  changed: (location: string) => void;
}) {
  const { api } = useApp();
  const [document, setDocument] = useState<PDFDocumentProxy>();
  const [page, setPage] = useState(Math.max(1, Number(initial) || 1));
  const [zoom, setZoom] = useState(1);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(true);
  const [text, setText] = useState('');
  const canvas = useRef<HTMLCanvasElement>(null);
  const container = useRef<HTMLDivElement>(null);
  const onChange = useRef(changed);
  useEffect(() => {
    onChange.current = changed;
  }, [changed]);
  useEffect(() => {
    let cancelled = false;
    const task = pdfjs.getDocument({
      url: api.url(`Items/${id}/File`).href,
      httpHeaders: api.requestHeaders(),
      enableXfa: false,
      useSystemFonts: true,
    });
    void task.promise
      .then((doc) => {
        if (cancelled) return;
        setDocument(doc);
        setPage((value) => Math.min(value, doc.numPages));
      })
      .catch(() => {
        if (!cancelled) {
          setError(
            'This PDF could not be opened. It may be unsupported, damaged or password protected.',
          );
          setBusy(false);
        }
      });
    return () => {
      cancelled = true;
      void task.destroy();
    };
  }, [api, id]);
  useEffect(() => {
    if (!document) return;
    let cancelled = false;
    let render: RenderTask | undefined;
    setBusy(true);
    setError('');
    void (async () => {
      const pdfPage = await document.getPage(page);
      if (cancelled) return;
      const element = canvas.current!;
      const normal = pdfPage.getViewport({ scale: 1 });
      const scale = Math.min(
        (Math.max(320, container.current!.clientWidth - 24) / normal.width) * zoom,
        Math.sqrt(10000000 / (normal.width * normal.height)),
      );
      const viewport = pdfPage.getViewport({ scale });
      element.width = Math.ceil(viewport.width);
      element.height = Math.ceil(viewport.height);
      render = pdfPage.render({ canvas: element, viewport });
      await render.promise;
      const content = await pdfPage.getTextContent();
      if (!cancelled) {
        setText(content.items.map((item) => ('str' in item ? item.str : '')).join(' '));
        setBusy(false);
        onChange.current(String(page));
      }
    })().catch((error) => {
      if (!cancelled && !(error instanceof pdfjs.RenderingCancelledException)) {
        setError('This PDF page could not be rendered.');
        setBusy(false);
      }
    });
    return () => {
      cancelled = true;
      render?.cancel();
    };
  }, [document, page, zoom]);
  return (
    <div className="pdf-reader">
      <div className="reader-toolbar">
        <button disabled={!document || page <= 1} onClick={() => setPage(page - 1)}>
          Previous page
        </button>
        <label>
          Book page
          <input
            type="number"
            min="1"
            max={document?.numPages ?? 1}
            value={page}
            onChange={(e) =>
              setPage(Math.max(1, Math.min(Number(e.target.value), document?.numPages ?? 1)))
            }
          />
        </label>
        <span>of {document?.numPages ?? '…'}</span>
        <button disabled={!document || page >= document.numPages} onClick={() => setPage(page + 1)}>
          Next page
        </button>
        <label>
          Page zoom
          <select value={zoom} onChange={(e) => setZoom(Number(e.target.value))}>
            <option value="0.75">75%</option>
            <option value="1">Fit width</option>
            <option value="1.25">125%</option>
            <option value="1.5">150%</option>
          </select>
        </label>
      </div>
      <Feedback busy={busy} error={error} />
      <details>
        <summary>Page text</summary>
        <p>{text || 'This page contains no extractable text.'}</p>
      </details>
      <div className="pdf-canvas" ref={container}>
        <canvas ref={canvas} aria-label={'PDF page ' + page} />
      </div>
    </div>
  );
}
