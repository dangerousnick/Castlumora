import DOMPurify from 'dompurify';

// Metadata is untrusted. Permit formatting only, with no network-bearing elements or attributes.
export function Overview({ text }: { text?: string | null }) {
  if (!text) return null;
  const html = DOMPurify.sanitize(text, {
    ALLOWED_TAGS: ['p', 'br', 'b', 'strong', 'em', 'i', 'ul', 'ol', 'li', 'blockquote'],
    ALLOWED_ATTR: [],
    ALLOW_DATA_ATTR: false,
    ALLOW_ARIA_ATTR: false,
  });
  return (
    <div
      className="metadata-overview"
      style={{ whiteSpace: 'pre-line' }}
      dangerouslySetInnerHTML={{ __html: html }}
    />
  );
}
