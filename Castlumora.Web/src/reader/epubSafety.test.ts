// @vitest-environment jsdom
import { expect, it } from 'vitest';
import JSZip from 'jszip';
import { checkEpubBounds, prepareEpub } from './epubSafety';
it('removes executable/remote EPUB content before rendering and retains readable chapter text', async () => {
  const zip = new JSZip();
  zip.file('mimetype', 'application/epub+zip');
  zip.file(
    'chapter.xhtml',
    '<?xml version="1.0"?><html xmlns="http://www.w3.org/1999/xhtml"><head><title>Castle</title><style>@import "https://tracker.example/x.css";body{background:url(https://tracker.example/x.png)}</style></head><body onload="alert(1)"><h1>Readable castle</h1><script>alert(1)</script><iframe src="https://tracker.example/"></iframe><img src="https://tracker.example/x.png" onerror="alert(2)"/><a href="javascript:alert(3)">Safe text</a></body></html>',
  );
  zip.file('run.js', 'alert(1)');
  const prepared = await prepareEpub(await zip.generateAsync({ type: 'arraybuffer' }));
  const output = await JSZip.loadAsync(prepared);
  const chapter = await output.file('chapter.xhtml')!.async('string');
  expect(chapter).toContain('Readable castle');
  expect(chapter).toContain('Content-Security-Policy');
  expect(chapter).not.toMatch(/<script|<iframe|onload=|onerror=|javascript:|https:\/\/tracker/i);
  expect(output.file('run.js')).toBeNull();
});
it('rejects traversing resource paths and excessive expansion before decompression', async () => {
  const zip = new JSZip();
  zip.file('../escape.xhtml', 'unsafe');
  expect(() => checkEpubBounds(new ArrayBuffer(0))).toThrow();
  const bytes = await zip.generateAsync({ type: 'arraybuffer' });
  expect(() => checkEpubBounds(bytes)).toThrow('Unsafe EPUB resource path');
  const safe = new JSZip();
  safe.file('chapter.xhtml', 'small');
  const inflated = await safe.generateAsync({ type: 'arraybuffer' });
  const view = new DataView(inflated);
  for (let offset = 0; offset < inflated.byteLength - 46; offset++)
    if (view.getUint32(offset, true) === 0x02014b50) {
      view.setUint32(offset + 24, 200 * 1024 * 1024, true);
      break;
    }
  expect(() => checkEpubBounds(inflated)).toThrow('safe reader limits');
});
it('rejects metadata that would fetch external resources before sandboxing a chapter', async () => {
  const zip = new JSZip();
  zip.file(
    'book.opf',
    '<package><manifest><item href="https://tracker.example/cover.jpg"/></manifest></package>',
  );
  await expect(prepareEpub(await zip.generateAsync({ type: 'arraybuffer' }))).rejects.toThrow(
    'External EPUB resources',
  );
});
