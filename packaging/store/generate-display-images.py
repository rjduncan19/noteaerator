"""Generate Microsoft Store display images for Note Aerator.

Produces, all from the canonical SVG icon:
  Box art (1:1):        1080x1080, 2160x2160
  Poster art (9:16):     720x1280, 1080x1920
  App tile icon (1:1):    300x300, 1080x1080, 2160x2160

All images are PNG, dark slate background (#1e293b) matching the package
tile BackgroundColor, with the decanter mark and the "Note Aerator"
wordmark + tagline.
"""

from __future__ import annotations

import io
from pathlib import Path

import resvg_py
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
SVG_PATH = ROOT / "POC" / "icons" / "12-decanter.svg"
OUT = Path(__file__).resolve().parent / "display"
OUT.mkdir(exist_ok=True)

BG = (30, 41, 59)             # #1e293b
INK = (248, 250, 252)         # #f8fafc
MUTED = (148, 163, 184)       # #94a3b8
ACCENT = (251, 146, 60)       # warm amber, plays off the decanter glow

FONT_TITLE = r"C:\Windows\Fonts\segoeuib.ttf"     # Segoe UI Bold
FONT_BODY = r"C:\Windows\Fonts\segoeui.ttf"


def render_svg(size: int) -> Image.Image:
    """Render the source SVG at size x size."""
    svg = SVG_PATH.read_text(encoding="utf-8")
    png_bytes = resvg_py.svg_to_bytes(svg_string=svg, width=size, height=size)
    return Image.open(io.BytesIO(bytes(png_bytes))).convert("RGBA")


def load_font(path: str, size: int) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(path, size=size)


def draw_centered_text(
    draw: ImageDraw.ImageDraw,
    text: str,
    font: ImageFont.FreeTypeFont,
    cx: int,
    y: int,
    fill,
) -> int:
    """Draw text horizontally centered on cx with top at y. Returns text height."""
    bbox = draw.textbbox((0, 0), text, font=font)
    w = bbox[2] - bbox[0]
    h = bbox[3] - bbox[1]
    draw.text((cx - w // 2 - bbox[0], y - bbox[1]), text, font=font, fill=fill)
    return h


def compose_hero(width: int, height: int, *, square: bool) -> Image.Image:
    """Compose the standard hero image used for box art and posters.

    Layout (top to bottom, centered):
        - decanter mark
        - "Note Aerator" wordmark
        - tagline
        - thin accent rule
    """
    img = Image.new("RGB", (width, height), BG)
    draw = ImageDraw.Draw(img)

    # Subtle radial-ish vignette using a top-of-canvas highlight rectangle.
    overlay = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    od = ImageDraw.Draw(overlay)
    for i in range(0, height // 3, 2):
        a = int(28 * (1 - i / (height / 3)))
        od.rectangle([0, i, width, i + 2], fill=(56, 78, 110, a))
    img.paste(overlay, (0, 0), overlay)
    draw = ImageDraw.Draw(img)

    # Decanter mark size + placement
    mark_size = int(min(width, height) * (0.46 if square else 0.42))
    mark = render_svg(mark_size)
    if square:
        mark_y = int(height * 0.18)
    else:
        mark_y = int(height * 0.18)
    img.paste(mark, ((width - mark_size) // 2, mark_y), mark)

    cx = width // 2

    # Title
    title_size = int(min(width, height) * (0.095 if square else 0.085))
    title_font = load_font(FONT_TITLE, title_size)
    title_y = mark_y + mark_size + int(height * 0.04)
    th = draw_centered_text(draw, "Note Aerator", title_font, cx, title_y, INK)

    # Tagline
    tag_size = int(title_size * 0.42)
    tag_font = load_font(FONT_BODY, tag_size)
    tag_y = title_y + th + int(height * 0.025)
    th2 = draw_centered_text(
        draw, "AI-first Markdown viewer", tag_font, cx, tag_y, MUTED
    )

    # Accent rule
    rule_y = tag_y + th2 + int(height * 0.035)
    rule_w = int(width * 0.18)
    rule_h = max(3, int(height * 0.006))
    draw.rectangle(
        [cx - rule_w // 2, rule_y, cx + rule_w // 2, rule_y + rule_h],
        fill=ACCENT,
    )

    # Footer line (poster only — square is tighter)
    if not square:
        foot_size = int(tag_size * 0.78)
        foot_font = load_font(FONT_BODY, foot_size)
        draw_centered_text(
            draw,
            "Read.  Browse.  Stay in flow.",
            foot_font,
            cx,
            height - int(height * 0.09),
            MUTED,
        )

    return img


def compose_tile(size: int) -> Image.Image:
    """Square app-tile icon: just the mark on bg, comfortable padding."""
    img = Image.new("RGB", (size, size), BG)
    pad_ratio = 0.14
    inner = int(size * (1 - 2 * pad_ratio))
    mark = render_svg(inner)
    img.paste(mark, ((size - inner) // 2, (size - inner) // 2), mark)
    return img


def main() -> None:
    targets: list[tuple[str, callable]] = [
        # Box art 1:1
        ("BoxArt-1080x1080.png", lambda: compose_hero(1080, 1080, square=True)),
        ("BoxArt-2160x2160.png", lambda: compose_hero(2160, 2160, square=True)),
        # Poster art 9:16
        ("PosterArt-720x1280.png", lambda: compose_hero(720, 1280, square=False)),
        ("PosterArt-1080x1920.png", lambda: compose_hero(1080, 1920, square=False)),
        # App tile 1:1
        ("AppTile-300x300.png", lambda: compose_tile(300)),
        ("AppTile-1080x1080.png", lambda: compose_tile(1080)),
        ("AppTile-2160x2160.png", lambda: compose_tile(2160)),
    ]
    for name, fn in targets:
        out = OUT / name
        img = fn()
        img.save(out, "PNG", optimize=True)
        kb = out.stat().st_size // 1024
        print(f"  {name:30s} {img.size[0]:>4}x{img.size[1]:<4} {kb:>5} KB")


if __name__ == "__main__":
    main()
