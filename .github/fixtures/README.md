# Native media fixtures for CI

This directory pins CI-only binaries used by the actual importer inspection tests.
It does not add a production downloader, executable override, or application bundle.
The existing server-configured FFmpeg/FFprobe integration is unchanged.

Linux's package-manager setup was cancelled after over16minutes because the hosted
runner could not resolve azure.archive.ubuntu.com, archive.ubuntu.com or
security.ubuntu.com. Builds/tests were never reached on that Linux job; this is
infrastructure failure, not a passing check or evidence of a decoder defect.
Windows lacked the native fixture; its advisory job failed before testing. macOS
completed the actual native tests with its installed tools.

`native-media.json` records a reviewed **dated** yt-dlp/FFmpeg-Builds release,
archive sizes, SHA256 digests and build/source references. Both pins agree with the
GitHub asset digest and dated publisher checksum file. The Linux archive was
actually downloaded/verified and its binaries executed locally; the complete API
suite then passed with those binaries. Windows native execution remains unverified
until its advisory job runs. No rolling latest URL or runtime updater is used.

The stdlib installer downloads with TLS verification, exact size/hash checks and
bounded time/bytes. It extracts only two exact regular-file names and a license to
a fresh private directory, rejects links/duplicate selected entries, checks native
versions with deadlines, and writes source provenance. It never extracts archive
paths or overwrites an existing tool directory. Version output retains upstream
copyright; included LICENSE.txt and source/build links retain GPL provenance.
These CI dependencies are not copied into the Castlumora application preview.

The workflow prepares fixtures after required Debug/Release builds, so a Windows
fixture issue still preserves a useful compatibility build. Setup is limited to
ten minutes, and all original test/assertion/coverage requirements remain. Linux
and macOS remain mandatory. macOS uses its installed FFmpeg or the existing
Homebrew formula if missing, within the same deadline; versions are logged.

For a deliberate pin update: review a dated publisher release/build/source, compare
its API digest against checksums.sha256, record size/root/hash in the manifest,
run the installer and real native API tests, then run the entire pre-push gate and
inspect exact-source CI. Upstream asset removal/expiry must fail clearly and
requires another reviewed pin; never silently use latest, ignore failed checks,
turn off TLS, relax native assertions or bypass missing fixtures.
