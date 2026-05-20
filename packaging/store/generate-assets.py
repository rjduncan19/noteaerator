"""
Generate the full Microsoft Store / MSIX visual-asset set for Note Aerator
from a single source SVG.

Usage:
    python generate-assets.py [<source-svg>]   (default: POC/icons/12-decanter.svg)

Produces, into packaging/store/Assets/:

  - Square44x44Logo.scale-100/125/150/200/400.png
  - Square44x44Logo.targetsize-{16,24,32,48,256}.png  (plated)
  - Square44x44Logo.targetsize-{16,24,32,48,256}_altform-unplated.png
  - Square71x71Logo.scale-100/125/150/200/400.png
  - Square150x150Logo.scale-100/125/150/200/400.png   (Medium tile)
  - Square310x310Logo.scale-100/125/150/200/400.png   (Large tile)
  - Wide310x150Logo.scale-100/125/150/200/400.png
  - SplashScreen.scale-100/125/150/200/400.png
  - StoreLogo.scale-100/125/150/200/400.png
  - LockScreenLogo.scale-200.png

Plus, into packaging/store/listing/:

  - StoreLogo-300x300.png   (300x300 logo used in the Partner Center listing)

Requires: resvg-py, Pillow (both already used by POC/icons/svg_to_ico.py).
"""
from __future__ import annotations

import sys
from io import BytesIO
from pathlib import Path

import resvg_py
from PIL import Image

REPO_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_SVG = REPO_ROOT / "POC" / "icons" / "12-decanter.svg"
ASSET_DIR = REPO_ROOT / "packaging" / "store" / "Assets"
LISTING_DIR = REPO_ROOT / "packaging" / "store" / "listing"

# Base (scale-100) sizes per Microsoft Store / MSIX visual-asset guidance.
# Each will be emitted at 1.00 / 1.25 / 1.50 / 2.00 / 4.00 scales.
SCALABLE = {
    "Square44x44Logo": (44, 44),
    "Square71x71Logo": (71, 71),
    "Square150x150Logo": (150, 150),
    "Square310x310Logo": (310, 310),
    "Wide310x150Logo": (310, 150),
    "SplashScreen": (620, 300),
    "StoreLogo": (50, 50),
}
SCALES = [100, 125, 150, 200, 400]

# Target-size variants for the Square44x44 set. Plated = on the tile background,
# unplated = transparent background (used in Start, taskbar, Alt-Tab, etc.).
TARGET_SIZES = [16, 24, 32, 48, 256]


def render_png(svg_text: str, width: int, height: int) -> Image.Image:
    """Rasterize SVG (assumed square 256 viewBox) into a (width, height) PNG.

    Non-square outputs (Wide tile, SplashScreen) get the square SVG centered
    horizontally with the rest filled by the SVG's outer rect color so the
    composition keeps breathing room rather than stretching.
    """
    # 12-decanter.svg has viewBox="0 0 256 256" and outer background #1e293b.
    # For non-square output, render square then composite onto a wider canvas.
    side = min(width, height)
    rendered = bytes(resvg_py.svg_to_bytes(
        svg_string=svg_text, width=side, height=side
    ))
    img = Image.open(BytesIO(rendered)).convert("RGBA")
    if width == height:
        return img
    # Center the square render on a larger canvas filled with the icon's bg.
    bg = (0x1e, 0x29, 0x3b, 0xff)
    canvas = Image.new("RGBA", (width, height), bg)
    x = (width - side) // 2
    y = (height - side) // 2
    canvas.alpha_composite(img, (x, y))
    return canvas


def render_unplated(svg_text: str, size: int) -> Image.Image:
    """Render the icon's foreground (decanter) onto a transparent background
    by skipping the outer rect. Crude but effective: we render normally and
    then knock out the dominant background color.
    """
    img = render_png(svg_text, size, size).convert("RGBA")
    bg = (0x1e, 0x29, 0x3b)
    px = img.load()
    w, h = img.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            # Tolerance for resvg's color rounding at small sizes.
            if abs(r - bg[0]) <= 4 and abs(g - bg[1]) <= 4 and abs(b - bg[2]) <= 4:
                px[x, y] = (0, 0, 0, 0)
    return img


def main(argv: list[str]) -> int:
    src_path = Path(argv[1]).resolve() if len(argv) > 1 else DEFAULT_SVG
    if not src_path.is_file():
        print(f"ERROR: source SVG not found: {src_path}", file=sys.stderr)
        return 1

    svg_text = src_path.read_text(encoding="utf-8")

    ASSET_DIR.mkdir(parents=True, exist_ok=True)
    LISTING_DIR.mkdir(parents=True, exist_ok=True)

    print(f"Source: {src_path.relative_to(REPO_ROOT)}")
    print(f"Output: {ASSET_DIR.relative_to(REPO_ROOT)}/")

    # 1. Scalable visual assets — every (asset, scale) pair.
    for name, (bw, bh) in SCALABLE.items():
        for scale in SCALES:
            w = round(bw * scale / 100)
            h = round(bh * scale / 100)
            out = ASSET_DIR / f"{name}.scale-{scale}.png"
            render_png(svg_text, w, h).save(out, format="PNG")
            print(f"  {out.name}  {w}x{h}")

    # 2. Square44x44 target-size variants (plated + unplated).
    for ts in TARGET_SIZES:
        # plated = on tile background
        out = ASSET_DIR / f"Square44x44Logo.targetsize-{ts}.png"
        render_png(svg_text, ts, ts).save(out, format="PNG")
        print(f"  {out.name}  {ts}x{ts}")
        # unplated = transparent background (taskbar, alt-tab, start search)
        out = ASSET_DIR / f"Square44x44Logo.targetsize-{ts}_altform-unplated.png"
        render_unplated(svg_text, ts).save(out, format="PNG")
        print(f"  {out.name}  {ts}x{ts} (unplated)")

    # 3. LockScreenLogo (24x24 base @ scale-200 = 48x48).
    lock = ASSET_DIR / "LockScreenLogo.scale-200.png"
    render_png(svg_text, 48, 48).save(lock, format="PNG")
    print(f"  {lock.name}  48x48")

    # 4. Partner Center listing 300x300 logo.
    listing_logo = LISTING_DIR / "StoreLogo-300x300.png"
    render_png(svg_text, 300, 300).save(listing_logo, format="PNG")
    print(f"  listing/{listing_logo.name}  300x300")

    print(f"\nDone. Generated assets are committed to git; regenerate with:")
    print(f"  python packaging/store/generate-assets.py")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
