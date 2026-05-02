"""
Convert an SVG to a multi-resolution Windows .ico using resvg-py + Pillow.

Usage:
    python svg_to_ico.py <input.svg> <output.ico>

Renders the SVG at 16, 24, 32, 48, 64, 128, 256 px and packs them into
a single ICO. Used by the noteaerator POC to generate app.ico from an
SVG under POC/icons/.
"""
from __future__ import annotations

import sys
from io import BytesIO
from pathlib import Path

import resvg_py
from PIL import Image


SIZES = [16, 24, 32, 48, 64, 128, 256]


def render(svg_path: Path, size: int) -> Image.Image:
    svg_text = svg_path.read_text(encoding="utf-8")
    png_bytes = bytes(resvg_py.svg_to_bytes(
        svg_string=svg_text,
        width=size,
        height=size,
    ))
    img = Image.open(BytesIO(png_bytes)).convert("RGBA")
    return img


def main(argv: list[str]) -> int:
    if len(argv) != 3:
        print(__doc__, file=sys.stderr)
        return 2
    svg_path = Path(argv[1]).resolve()
    ico_path = Path(argv[2]).resolve()
    if not svg_path.is_file():
        print(f"ERROR: {svg_path} not found", file=sys.stderr)
        return 1

    images = [render(svg_path, s) for s in SIZES]
    base = images[-1]  # 256
    base.save(
        ico_path,
        format="ICO",
        sizes=[(img.width, img.height) for img in images],
        append_images=images[:-1],
    )
    print(f"wrote {ico_path} ({len(images)} sizes: {SIZES})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
