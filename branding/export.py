"""Export the Castlumora SVG with CairoSVG and Pillow (development tools only).

Run from repository root: python branding/export.py
Install CairoSVG and Pillow in a temporary virtual environment first.
"""
from pathlib import Path
import re

import cairosvg
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "branding"
cairosvg.svg2png(
    url=str(ASSETS / "castlumora.svg"),
    write_to=str(ASSETS / "castlumora-1024.png"),
    output_width=1024,
    output_height=1024,
)
with Image.open(ASSETS / "castlumora-1024.png") as source:
    icon = source.convert("RGBA")
icon.save(ASSETS / "castlumora-1024.png", optimize=True)
for size in (16, 32, 48, 64, 128, 180, 192, 256, 512):
    icon.resize((size, size), Image.Resampling.LANCZOS).save(
        ASSETS / f"castlumora-{size}.png", optimize=True
    )
icon.save(
    ROOT / "Jellyfin.Server/Castlumora.Server.ico",
    format="ICO",
    sizes=[(size, size) for size in (16, 24, 32, 48, 64, 128, 256)],
)
svg = (ASSETS / "castlumora.svg").read_text()
# The wordmark is intentionally live text, with a system serif fallback.
banner = svg.replace('viewBox="0 0 256 256"', 'viewBox="0 0 900 256"').replace('</svg>', '<text x="265" y="155" fill="#ecccff" font-family="Georgia,serif" font-size="91">Castlumora</text></svg>')
(ASSETS / "castlumora-wordmark.svg").write_text(banner)
cairosvg.svg2png(bytestring=banner.encode(), write_to=str(ASSETS / "castlumora-wordmark-1800.png"), output_width=1800, output_height=512)
with Image.open(ASSETS / "castlumora-wordmark-1800.png") as wordmark:
    wordmark.save(ASSETS / "castlumora-wordmark-1800.png", optimize=True)
(ROOT / "Jellyfin.Server/wwwroot/api-docs/castlumora.svg").write_text(banner)
# Retain the legacy asset URL for cached Swagger stylesheets.
(ROOT / "Jellyfin.Server/wwwroot/api-docs/jellyfin.svg").write_text(banner)

startup = ROOT / "Jellyfin.Server/ServerSetupApp/index.mstemplate.html"
inline = svg.strip().replace('viewBox=', 'class="castle-mark" viewBox=')
inline = inline.replace('id="title"', 'id="castle-title"').replace(
    'aria-labelledby="title"', 'aria-labelledby="castle-title"'
)
startup.write_text(re.sub(r'<svg.*?</svg>', lambda _: inline, startup.read_text(), count=1, flags=re.S))
