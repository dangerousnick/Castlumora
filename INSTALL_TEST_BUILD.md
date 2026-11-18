# Install Castlumora 1.0 Development Preview (Linux x64)

This is a hands-on development preview, not a stable release or full feature parity. Use **Linux Mint 22.3 / Ubuntu 24.04-compatible x86-64**, a current Firefox/Chromium browser and at least 2 GiB free on the data/config/log/cache filesystems (allow extra space for media/transcodes). The archive includes the .NET 10 runtime and compiled Castlumora Web. No Node, SDK, Git checkout or frontend compilation is required on the test computer. Actual verification is on the documented Linux build host; Mint hardware/device certification remains pending.

## Download and verify

The primary delivery is a **GitHub Actions artifact**: open https://github.com/dangerousnick/Castlumora/actions/workflows/castlumora-preview.yml, select a successful **Castlumora Linux Development Preview** run, and scroll to **Artifacts**. Sign in to GitHub, then download `Castlumora-linux-x64-<full-source-commit>`. Actions artifacts expire after 30 days; rebuild the documented source if an older artifact expires. The PR and progress file link the exact verified run/artifact.

The downloaded outer ZIP contains `Castlumora-1.0-dev-<commit>-linux-x64.tar.gz`, its `.sha256`, `BUILD_INFO.json`, this guide, the feature checklist and `CI_SMOKE_REPORT.md`. Extract that ZIP first through Linux Mint's archive manager; then verify and extract the inner `.tar.gz` as described below. The manifest, archive name and CI report identify the exact tested source commit.

If a public prerelease is separately published at https://github.com/dangerousnick/Castlumora/releases, its archive/checksum may be downloaded directly. Do not assume an empty draft release is a delivered build: the workspace upload host rejected its credential, so the GitHub-hosted read-only artifact workflow is used instead.
In a terminal in Downloads, use the actual downloaded filename:

```bash
cd ~/Downloads
sha256sum --check Castlumora-1.0-dev-<commit>-linux-x64.tar.gz.sha256
```

Expected: `OK`. Do not launch a corrupted archive. Linux Mint's archive manager can extract the `.tar.gz` through **Extract Here**; put the extracted folder somewhere you own, such as `~/Applications`. Keep its server/web/licenses/branding folders together. Avoid system installation directories and never replace an existing media server.

## Dependencies

Install the native Linux libraries and FFmpeg through Software Manager, or:

```bash
sudo apt update
sudo apt install ffmpeg libicu74 libssl3t64 libfontconfig1 libfreetype6
```

Ubuntu/Mint package versions vary; use their corresponding ICU/OpenSSL 3 packages if those exact names differ. The bundled .NET runtime still requires native system libraries. FFmpeg and its sibling ffprobe are external, not bundled. New previews bundle separately licensed pinned yt-dlp and Node 24 runtimes (including their notices); The development YouTube single-video form is available with the reviewed Linux x64 dependencies; real public-source access remains unverified in this workspace (HTTP403), and it is not production-certified. No ambient Node executable or downloaded JavaScript components are used. The Node dependency is included for that reviewed source adapter. See CASTLUMORA_YOUTUBE_IMPORT.md for preview/rights confirmation, quality and per-job size budgets, recovery and testing limitations. Install/check the yt-dlp runtime through Administration → Imports & Sources. Previously, previews only bundled yt-dlp. The verified integrated preview adds a persistent authorized single direct HTTPS MP4 queue (32MiB, H264/AAC, one worker) when native media dependencies are ready. See the included CASTLUMORA_IMPORTER_RUNTIME.md and CASTLUMORA_SINGLE_VIDEO_IMPORT.md. Normal FFmpeg validation stays enabled; the package was tested with FFmpeg 7.1.5. The inherited minimum recommendation is 4.4; ordinary supported Mint FFmpeg may work, but every codec/hardware path is not certified. A compatible `jellyfin-ffmpeg` installation can be selected explicitly:

```bash
CASTLUMORA_FFMPEG=/usr/lib/jellyfin-ffmpeg/ffmpeg ./start-castlumora
```

Do not disable validation to hide missing codec/probe errors. System FFmpeg carries its own distributor's license notices.

## Start and initialize

Open the extracted folder in Files, then **Open in Terminal**, and run:

```bash
./start-castlumora
```

A terminal remains open with status. Open **http://127.0.0.1:28096/web/**. Castlumora Web is served by the packaged server; there is no Vite/development frontend server. The native setup page creates your administrator password and server name. Choose your own password; no test credentials are shipped. Then sign in. Initial setup disables remote access.

`Castlumora.desktop` offers an optional terminal-launch shortcut when your desktop permits it; mark it executable/trusted through its Properties if required. Running `start-castlumora` directly is the reliable fallback.

Preview state defaults to `~/.local/share/castlumora-preview` (or `$XDG_DATA_HOME/castlumora-preview`). It is separate from system Jellyfin/Castlumora/Coon Media directories. The launcher refuses an existing nonempty directory without its own `.castlumora-preview` marker. **Never point it at production data/configuration.** It keeps temporary files beneath its own cache and disables discovery on fresh installs. It does not install a service, modify another server or start at login. Default binding is 127.0.0.1 on port 28096. Choose a different unused port or fresh data root on first launch if necessary:

```bash
CASTLUMORA_PORT=28097 CASTLUMORA_DATA_DIR="$HOME/.local/share/castlumora-preview-other" ./start-castlumora
```

Once network.xml exists, use Administration → System → Networking to change its port/binding; the environment variable does not silently rewrite saved settings. LAN access/HTTPS/reverse proxies need deliberate administrator configuration and are not certified by the local preview tests. Stop the preview before replacing its archive.

## Libraries, accounts and implemented features

Sign in as administrator → **Administration → Libraries**. Add a readable folder you own, select Movies/Television/Music/Photos/Books and scan. Photos map to the supported home-video library type with photo indexing enabled. Existing files are read by the inherited backend; do not experiment with deletion on originals. For safe testing, use copies or synthetic/public-domain content. Browse lists Movies/TV, music, photos, books and collections; Home and Search use permitted catalog items. Movies/episodes expose playback, quality/tracks/chapters and progress.

Administration → Users manages separate server users, passwords and access policies. Kids/adult named presets and multiple linked viewing profiles per account are **unfinished**; the profile menu currently switches authenticated server users. Administration → Registration manages optional signup and invitations; keep signup closed unless intentionally testing it. All modes use the same free features.

Live TV → Guide/Scheduled/Series/Recordings uses the inherited backend; Administration → Live TV configures M3U/XMLTV/tuners. You must provide an authorized working source; no external channels are bundled. Administration → Skipper configures optional analysis and review. Suggestions must be approved before skip controls appear. Real-series accuracy, GPU acceleration and native TV/mobile are not certified. See TEST_BUILD_CHECKLIST.md and the verification report linked in the release.

## Advanced administration and legacy client limitation

Native administration includes libraries, users/policies/devices, encoding/hardware settings, network/general settings, plugins/repositories/catalog, tasks/triggers/logs, backup actions and tuner/XMLTV/mapping screens. Some advanced inherited operations still need a compatible legacy Web distribution. **This package does not include legacy Web assets.** Its Existing client link opens an honest help page instead of looping back into Castlumora. The inspected upstream v12.4 GitHub Web release has no downloadable assets; an older arbitrary client is not silently substituted.

If you already have a trusted **compatible compiled** legacy Web distribution, stop the preview, remove only the packaged help directory, and attach those static assets:

```bash
# Run from the extracted preview directory; this removes only its static help page.
rm web/legacy/index.html
rmdir web/legacy
CASTLUMORA_LEGACY_WEB_DIR="/absolute/path/to/compatible/compiled/web" ./start-castlumora
```

The launcher validates index.html and creates a static symlink at `/web/legacy/`. It never reads another server's databases/configuration or modifies that distribution. The legacy client may need its own login and may use base-path assumptions requiring configuration; test compatibility before relying on it. If no compatible distribution is available, unfinished advanced web controls remain unavailable in this preview; inherited backend APIs are retained. This limitation prevents a claim of full administrative parity. API documentation remains at `/api-docs/swagger/`.

## Stop, restart, logs and recovery

Press **Ctrl+C** in the server terminal; wait for shutdown before restarting `./start-castlumora`. Accounts, layouts, libraries and preferences persist in the preview state directory. Do not run two preview processes against one data directory. Do not force-kill a shared/system FFmpeg process.

If startup fails, read the terminal and `~/.local/share/castlumora-preview/logs/`. Common causes: port occupied, less than 2 GiB free, missing FFmpeg/ffprobe or native libraries, unreadable media folders, duplicate process, unsupported codecs or stale browser cache. `ldd server/jellyfin` and `server/jellyfin --version` help check native dependencies. Failed password setup can be retried in the same page; if credentials already exist the Finish setup action does not overwrite them. After completion, setup APIs retain their inherited authorization restrictions.

To collect diagnostics, stop playback, note the time, copy the relevant log interval and BUILD_INFO.json, browser console/network error, OS/browser/FFmpeg version and reproduction steps. Logs may contain usernames, paths or media URLs; redact passwords/tokens and personal information before reporting at https://github.com/dangerousnick/Castlumora/issues. Never post an entire config/database or reusable invitation token.

Back up the preview state directory while the server is stopped; the `config/castlumora` subtree contains presentation/invitations/reader/skipper state. Real restore and production migration are not certified. To remove: stop the preview, delete the extracted application folder through Files, then delete **only your preview state directory** if you no longer want its test accounts/state. Media outside that directory is not removed by uninstalling the application. Do not delete an existing Coon Media/Jellyfin data directory.

## Scope and reproducibility

The initial built-in direct-video importer is a bounded development workflow, **not complete production importing**. Use Administration → Imports & Sources for eligible Home Videos libraries, rights-confirmed direct MP4 submission, history, cancel and retry. Single YouTube jobs now have preview/rights/destination confirmation, persistent queueing, bounded local remuxing and creator provenance. Their owned-media/native/API contracts pass tests; real external YouTube extraction/download remains unverified because source access was denied. Playlists/channels/scheduled sources, full bulk migration and the complete independent online YouTube provider remain unfinished. The native offline .info.json reader identifies existing generic videos, and Administration → Metadata Management supplies local-reader settings, selected-item corrections/locks/confirmed repair. See the included CASTLUMORA_CREATOR_SIDECAR.md and CASTLUMORA_METADATA_MANAGEMENT.md. Synthetic DVR completion/playback, active stop/retained partial media and permission-checked deletion have been verified; real tuners, long-duration behavior and quotas remain unverified or unfinished. Other unfinished groups include linked profiles, recommendations/follows/notifications, resource tiers, creator/OBS/business/cloud/offline/native clients and full metadata/ambiguity/bulk/artwork management. FEATURE_PARITY.md and PRODUCT_ROADMAP.md preserve them.

Developer rebuild (not required on the test computer): check out the release's exact source commit, install Node 24/npm and .NET SDK 10.0.x, then run `packaging/linux/build-preview.sh` from the repository. It requires a clean checkout, npm lockfile, self-contained linux-x64 publish, a fresh output directory and produces a commit-named archive/checksum/BUILD_INFO. Tar entry ordering/time/ownership are normalized; toolchain/native dependency versions can still affect binary bytes. GPL/upstream source history, LICENSE/CONTRIBUTORS and web dependency notices are retained. Exact sources are downloadable from the linked GitHub commit.

## Backup capacity and platform evidence

Creating a production backup still requires approximately 5 GiB free in its backup
filesystem. That safety guard is separate from the preview's startup requirement;
small automated fixtures use an internal isolated policy, never a user setting.
The general validation gate no longer imposes that reserve on every test.

This archive is Linux x64. macOS managed-tool CI and inherited server compatibility
are separate from a tested macOS desktop installer. Windows compatibility is
advisory and deferred for Phase 1.4/stable 1.0. See CASTLUMORA_PLATFORM_SUPPORT.md
inside the package for evidence and remaining checks. No broader support claim
follows merely from compiling source.
