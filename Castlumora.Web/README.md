# Castlumora Web 1.0 (development)

Independent React + TypeScript + Vite client for the existing server. No mandatory paid features. Licensed under the repository's GPL; upstream server attribution is preserved. This does not replace the inherited Web client.

## Development

Requires Node.js 24 LTS and npm. The repository `.nvmrc` selects Node 24 (`nvm use` from the repository root); the frontend package declares the same supported major. From this directory:

```sh
npm ci
npm run dev
npm run typecheck
npm run lint
npm test
npm run build
```

Open http://localhost:5173 and enter your server's origin, including its reverse-proxy base path if applicable. Finish initial server setup through the existing Web client first. Pairing saves only the server address and a generated device ID in localStorage. Access tokens are tab-scoped in sessionStorage and cleared on logout; passwords are never persisted. Profile switching logs out and requires the other user's credentials; public profiles are suggestions, not permission bypasses. Use HTTPS outside local development.

The added presentation endpoints require this branch's server build. The production `dist/` can be hosted as a separate static application. Relative assets and hash routing support a subdirectory. Configure the reverse proxy to serve it at a distinct path, for example `/castlumora/`, preserving `/web/` and the server API base. Do not replace the legacy Web installation. CORS must allow the frontend origin. Do not deploy development Vite servers to production.

Native video elements require token-bearing stream/subtitle URLs; API fetches use authorization headers. Configure access logs to redact query credentials and keep the no-referrer policy. Server-provided playback URLs are restricted to the paired server's origin and base path before credentials are attached.

## Current coverage

Login/logout, existing user profiles, movie/series browsing/search, details/seasons/episodes, resumable browser playback, server-negotiated HLS, audio/subtitle selection, progress reporting, Live TV channels/three-hour guide, separate server-backed watchlist and resume hiding, administrator overview and draft/publish presentation editor. Actual stream/browser coverage must be read in the verification report; source implementation does not imply every codec or tuner was verified.

Music/photos/books, library setup, tuners/XMLTV, DVR scheduling, hardware encoding, plugins, networking, backups and advanced user administration remain accessible through **Existing client** / **Full server administration** links. Legacy links open the installed client root to avoid assuming its router version. Sign in there and choose Dashboard or Live TV as needed. Its configured Web distribution must remain installed.

See root `DEVELOPMENT_PROGRESS.md` for current validation, known limitations and continuation instructions. Root `docs/CASTLUMORA_PRODUCT_REQUIREMENTS.md` is the permanent product specification.

## Distribution notices

The static build includes the unchanged project GPL text as `LICENSE.txt` and runtime dependency license texts as `THIRD_PARTY_NOTICES.txt`. Keep both files with distributions; update the notices when updating runtime dependencies. Server copyright and contributor files remain unchanged.

Native media guides: [music and private playlists](../docs/CASTLUMORA_MUSIC.md), [photos/books/collections and reader state](../docs/CASTLUMORA_READER_AND_SHELVES.md). The complete requirements and progress files distinguish implemented workflows from remaining work.
