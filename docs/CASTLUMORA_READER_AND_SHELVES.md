# Native photos, books and collections

Castlumora Web uses the existing permission-filtered item, image, file and collection APIs. No media paths, identifiers or database tables are renamed. Photo libraries use the server-supported `homevideos` type with `EnablePhotos: true`; the native administrator library form maps its Photos choice accordingly.

Photos support album/all-photo browsing, search, pagination, previous/next, rotation, zoom and a five-second slideshow. Books have their own shelf and native PDF/EPUB readers. Collections support creation, adding/removing existing media and deleting the collection while retaining its media. The server remains authoritative for all access and collection-management permissions. Download links appear only for profiles with download permission.

## Reading state

Authenticated GET/PUT `/Castlumora/Reader/{bookId}` reads or saves the caller's position and theme/font preferences. The controller requires an accessible Book; there is no caller-supplied profile ID. State is written atomically beneath `config/castlumora/reader/{userId}-{bookId}.json`. Include this directory in backups. This is single-server configuration storage; multiple server processes sharing a configuration directory are unsupported.

PDF pages use a locally bundled PDF.js worker and authenticated range requests. Only page canvas/text content is rendered; PDF scripts, interactive forms and annotations are not executed. EPUBs are fetched with authenticated headers, then rendered from sanitized local bytes without passing the server token to the renderer. Executable content and external metadata resources are rejected/removed, chapters have a restrictive content security policy, and EPUB scripting is disabled. Archive limits are 64 MiB input, 128 MiB declared expanded data, 32 MiB per resource and 5,000 entries; ZIP64 and traversing paths are rejected. Unsupported, encrypted and damaged books show an explicit error or external-reader fallback. EPUB CFI/PDF page positions are profile-specific; saves are serialized to preserve page/theme ordering.

PDF page text is available through the Page text disclosure. EPUB offers chapters, previous/next, theme and font size. This does not implement comics, AZW/MOBI conversion, full-text search, annotation synchronization, offline reading or native device applications. Large/complex book compatibility and assistive technology need broader real-library testing.

## Disposable verification

Only use the marked loopback fixture described in the verification report. `Castlumora.Web/browser/setup-shelf-fixture.py --fixture-root /tmp/castlumora-web-real-server` checks the marker and the server's actual data directory before creating synthetic images and a two-page PDF/two-chapter EPUB. It reads the existing `CASTLUMORA_TEST_SERVER`, `CASTLUMORA_TEST_USER` and `CASTLUMORA_TEST_PASSWORD` environment variables; never point it at a production server. Set `CASTLUMORA_TEST_SHELF=1` to opt into `browser/shelf-server.spec.ts`.

Runtime dependencies and their licenses are retained in the distributed `THIRD_PARTY_NOTICES.txt`; GPL/upstream attribution remains unchanged.
