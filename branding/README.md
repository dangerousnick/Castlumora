# Castlumora assets

The scalable mark adapts the user-supplied `3151.jpg` reference (preserved as `reference-castle.jpg`): purple crescent, three castle towers, flags and a play symbol. Fine film-strip and metallic lettering details are omitted from the small icon for legibility. This is a simplified vector adaptation, not an exact trace.

`castlumora.svg` is the scalable source. PNGs are transparent RGBA at 16, 32, 48, 64, 128, 180 (Apple touch), 192, 256, 512 and 1024 pixels. The transparent violet crescent and purple castle use no external fonts or images. The server ICO contains 16–256 pixel entries. PNGs use optimized lossless compression.

The wordmark uses a system serif font fallback and includes an optimized 1800 × 512 PNG. The startup page embeds the icon SVG to work before static hosting is ready. Swagger uses the wide wordmark SVG under `Jellyfin.Server/wwwroot/api-docs/`. The legacy `api-docs/jellyfin.svg` URL also serves the Castlumora wordmark for cached stylesheets. The export script synchronizes those copies when changing the design. The executable uses `Jellyfin.Server/Castlumora.Server.ico`; the upstream icon remains in source for provenance.

Regenerate exports with `python branding/export.py` using CairoSVG and Pillow. The export script also embeds the icon into the startup page with unique title IDs.

Palette: deep purple `#130721`, castle `#301052`, violet `#7020d6`–`#b52bff`, outline `#c06aff`, pale text `#ecccff`. Use the full mark at large sizes and the square icon for application shortcuts. No animation or external image/font downloads are needed at runtime.
