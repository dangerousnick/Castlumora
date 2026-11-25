# Managed yt-dlp dependency

Official release: https://github.com/yt-dlp/yt-dlp/releases/tag/2026.10.26

Castlumora includes the unmodified, pinned platform executable, release source archive, original LICENSE and complete upstream THIRD_PARTY_LICENSES.txt. `release.json` records exact platform/source hashes and lengths. The package does not self-update or accept arbitrary executable URLs, paths or CLI flags.

yt-dlp source is under the Unlicense, but the PyInstaller-bundled executables include GPLv3+ and other licensed components. The executable combination is distributed under GPLv3+, as documented by upstream; do not label the whole standalone binary Unlicense-only. Full license texts, attribution and upstream component/source locations are retained in THIRD_PARTY_LICENSES.txt, including upstream's corresponding-source contact. Release source and build/dependency material are available from the pinned release and repository tag. Preserve all these notices and source materials when redistributing the tool; Castlumora's existing GPL and upstream notices remain unchanged.

Source repository/tag: https://github.com/yt-dlp/yt-dlp/tree/2026.10.26

The managed-runtime milestone supports installation and offline integrity/version health only. Authorized media import queues, source-policy/egress protections and downloading are separate unfinished work. No account cookies, DRM bypass, updater, external plugins, or arbitrary commands are exposed by this milestone.
