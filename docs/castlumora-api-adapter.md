# Castlumora Web API adapter

All server communication lives in `Castlumora.Web/src/api/client.ts`, with typed response subsets in `types.ts`. Existing API names intentionally retain compatibility identifiers. Requests use the paired origin plus base path, existing `MediaBrowser` authorization and client/device/version identification; they never use an administrator token on behalf of another profile.

Authentication: public server info/profile suggestions, `Users/AuthenticateByName`, then `Users/Me`; logout calls `Sessions/Logout`. Users must authenticate separately when switching profiles. The setup wizard remains in the legacy client.

Browsing: `Users/{id}/Views`, `Items` with user ID, recursive filters, fields, search and paging; `Users/{id}/Items/{itemId}`; `Shows/{seriesId}/Seasons` and `/Episodes`; `UserItems/Resume`, `Shows/NextUp`. Categories translate saved rules to existing permission-filtered Items requests and merge results, without moving files. Public image URLs use server image tags; inaccessible item IDs must never grant access through the presentation schema.

Playback: POST `Items/{id}/PlaybackInfo` with conservative browser device profile, selected tracks, start ticks and transcoding capability. Use direct MP4 H264/AAC where negotiated; otherwise use returned HLS streaming URL. hls.js is lazy-loaded with the player; Safari can use native HLS. Native media/subtitle URLs need query authentication, so no-referrer and log redaction are essential. The adapter checks media URL origin and base path before appending credentials. Session reports use `Sessions/Playing`, `/Progress`, `/Stopped`. Teardown destroys only this player's hls.js object, reports its position and calls the existing `Videos/ActiveEncodings` and `LiveStreams/Close` APIs with negotiated IDs; no FFmpeg process killing.

Live TV: real `LiveTv/Channels` and `LiveTv/Programs` for the next three hours; channels feed the same player. Recordings/schedules/tuners/XMLTV remain in the existing client/dashboard. No tuner is configured automatically.

Administration: `System/Info`, `Users`, `Sessions`, and additive [presentation API](castlumora-presentation-v1.md). Buttons represent real persistence, not local-only settings. Unsupported advanced settings link to the existing dashboard and require its normal authorization.

Error handling: non-success responses become bounded messages without echoing server response bodies or tokens. 401 asks the user to sign in; 403 explains profile restrictions. Home rows fail independently; inaccessible featured media is skipped. Empty rows disappear. Presentation 404 uses defaults; other authorization failures are not hidden. No fake catalog or production demo mode is included. Isolated browser tests intercept explicitly named fixture responses.
