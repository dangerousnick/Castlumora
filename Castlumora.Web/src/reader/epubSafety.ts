import JSZip from 'jszip';
import DOMPurify from 'dompurify';
const MAX_INPUT = 64 * 1024 * 1024;
const MAX_EXPANDED = 128 * 1024 * 1024;
const CSP =
  "default-src 'none'; img-src blob: data:; style-src 'unsafe-inline' blob: data:; font-src blob: data:; media-src blob: data:; connect-src 'none'; script-src 'none'; form-action 'none'; frame-src 'none'; base-uri 'none'";
function local(value: string) {
  return (
    !/^(?:[a-z][a-z0-9+.-]*:|\/\/)/i.test(value.trim()) ||
    /^(?:blob:|data:image\/(?:png|jpeg|gif|webp);)/i.test(value.trim())
  );
}
function css(value: string) {
  return value
    .replace(/@import[^;]+;/gi, '')
    .replace(/url\(\s*(['"]?)(.*?)\1\s*\)/gi, (original, _quote: string, url: string) =>
      local(url) ? original : 'url()',
    );
}
export function checkEpubBounds(buffer: ArrayBuffer) {
  if (buffer.byteLength > MAX_INPUT || buffer.byteLength < 22)
    throw new Error('This EPUB exceeds the safe reader size or is incomplete.');
  const data = new DataView(buffer);
  let end = -1;
  for (let n = buffer.byteLength - 22; n >= Math.max(0, buffer.byteLength - 65557); n--)
    if (data.getUint32(n, true) === 0x06054b50) {
      end = n;
      break;
    }
  if (end < 0) throw new Error('Invalid EPUB archive.');
  const entries = data.getUint16(end + 10, true);
  const size = data.getUint32(end + 12, true);
  let offset = data.getUint32(end + 16, true);
  if (entries > 5000 || entries === 65535 || offset + size > end)
    throw new Error('This EPUB archive layout exceeds safe reader limits.');
  let expanded = 0;
  for (let n = 0; n < entries; n++) {
    if (offset + 46 > end || data.getUint32(offset, true) !== 0x02014b50)
      throw new Error('Invalid EPUB directory.');
    const length = data.getUint32(offset + 24, true);
    const nameLength = data.getUint16(offset + 28, true);
    const extra = data.getUint16(offset + 30, true);
    const comment = data.getUint16(offset + 32, true);
    if (
      length === 0xffffffff ||
      length > 32 * 1024 * 1024 ||
      (expanded += length) > MAX_EXPANDED ||
      offset + 46 + nameLength + extra + comment > end
    )
      throw new Error('This EPUB expands beyond safe reader limits.');
    const name = new TextDecoder()
      .decode(new Uint8Array(buffer, offset + 46, nameLength))
      .replaceAll('\\', '/');
    if (
      name.startsWith('/') ||
      name.includes('\0') ||
      name.split('/').some((part) => ['..', '__proto__', 'constructor', 'prototype'].includes(part))
    )
      throw new Error('Unsafe EPUB resource path.');
    offset += 46 + nameLength + extra + comment;
  }
}
export async function prepareEpub(buffer: ArrayBuffer): Promise<ArrayBuffer> {
  checkEpubBounds(buffer);
  const archive = await JSZip.loadAsync(buffer, { checkCRC32: true });
  for (const entry of Object.values(archive.files)) {
    if (entry.dir) continue;
    if (/\.(?:opf|ncx|xml)$/i.test(entry.name)) {
      const source = await entry.async('string');
      if (/<!DOCTYPE|<!ENTITY/i.test(source))
        throw new Error('Unsupported EPUB metadata declarations.');
      const metadata = new DOMParser().parseFromString(source, 'application/xml');
      if (metadata.querySelector('parsererror')) throw new Error('Malformed EPUB metadata.');
      for (const element of metadata.querySelectorAll('*'))
        for (const attribute of Array.from(element.attributes))
          if (
            ['href', 'src', 'full-path'].includes(attribute.localName) &&
            /^(?:[a-z][a-z0-9+.-]*:|\/\/)/i.test(attribute.value.trim())
          )
            throw new Error('External EPUB resources are not supported.');
    }
    if (/\.(?:xhtml|html|htm)$/i.test(entry.name)) {
      const source = (await entry.async('string')).replace(/<!DOCTYPE[^>]*>/gi, '');
      const doc = new DOMParser().parseFromString(source, 'application/xhtml+xml');
      if (doc.querySelector('parsererror')) throw new Error('Malformed EPUB chapter markup.');
      for (const element of doc.querySelectorAll('*')) {
        for (const attr of Array.from(element.attributes))
          if (
            /^on/i.test(attr.name) ||
            attr.name === 'srcdoc' ||
            (['src', 'href', 'xlink:href', 'poster', 'action'].includes(attr.name) &&
              !local(attr.value))
          )
            element.removeAttribute(attr.name);
        if (element.hasAttribute('style'))
          element.setAttribute('style', css(element.getAttribute('style')!));
      }
      for (const style of doc.querySelectorAll('style'))
        style.textContent = css(style.textContent ?? '');
      DOMPurify.sanitize(doc.documentElement, {
        IN_PLACE: true,
        WHOLE_DOCUMENT: true,
        USE_PROFILES: { html: true, svg: true },
        FORBID_TAGS: [
          'script',
          'iframe',
          'object',
          'embed',
          'form',
          'input',
          'button',
          'base',
          'meta',
          'foreignObject',
          'audio',
          'video',
        ],
        FORBID_ATTR: ['srcdoc'],
        ADD_TAGS: ['link'],
        ADD_ATTR: ['epub:type'],
      });
      const head = doc.querySelector('head');
      const policy = doc.createElementNS('http://www.w3.org/1999/xhtml', 'meta');
      policy.setAttribute('http-equiv', 'Content-Security-Policy');
      policy.setAttribute('content', CSP);
      head?.prepend(policy);
      archive.file(entry.name, new XMLSerializer().serializeToString(doc));
    } else if (/\.css$/i.test(entry.name))
      archive.file(entry.name, css(await entry.async('string')));
    else if (/\.(?:js|mjs|htmlz)$/i.test(entry.name)) archive.remove(entry.name);
  }
  return archive.generateAsync({ type: 'arraybuffer', compression: 'DEFLATE' });
}
