"""Generate the Thermalith app icon (PNG + multi-size ICO) from code, no design assets needed.

A rounded square with a warm thermal gradient (the printer heats paper), a white
label/tag mark, and a bold "T". Run with the bundled Roboto for the glyph.
"""
import os
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "src", "Thermalith.App", "Assets")
FONT = os.path.join(ROOT, "src", "Thermalith.Core", "Fonts", "Roboto-Regular.ttf")
os.makedirs(OUT, exist_ok=True)

S = 512
TOP = (255, 138, 24)    # warm orange
BOT = (196, 22, 28)     # deep red


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def build(size):
    # Vertical thermal gradient.
    grad = Image.new("RGB", (size, size))
    px = grad.load()
    for y in range(size):
        c = lerp(TOP, BOT, y / (size - 1))
        for x in range(size):
            px[x, y] = c

    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    img.paste(grad, (0, 0))

    # Rounded-square alpha mask.
    mask = Image.new("L", (size, size), 0)
    md = ImageDraw.Draw(mask)
    r = int(size * 0.20)
    md.rounded_rectangle([0, 0, size - 1, size - 1], radius=r, fill=255)
    img.putalpha(mask)

    d = ImageDraw.Draw(img)

    # White label/tag mark behind the letter.
    m = size * 0.20
    tag = [m, m * 1.15, size - m, size - m]
    d.rounded_rectangle(tag, radius=int(size * 0.07), fill=(255, 255, 255, 235))
    # Tag punch hole, top-left of the label.
    hole = size * 0.045
    hx, hy = m + size * 0.085, m * 1.15 + size * 0.085
    d.ellipse([hx - hole, hy - hole, hx + hole, hy + hole], fill=(196, 22, 28, 255))

    # Bold "T" in the brand red, centred on the label.
    try:
        font = ImageFont.truetype(FONT, int(size * 0.5))
    except OSError:
        font = ImageFont.load_default()
    text = "T"
    box = d.textbbox((0, 0), text, font=font)
    tw, th = box[2] - box[0], box[3] - box[1]
    tx = (size - tw) / 2 - box[0]
    ty = (size - th) / 2 - box[1] + size * 0.04
    d.text((tx, ty), text, font=font, fill=(196, 22, 28, 255))
    return img


master = build(S)
master.save(os.path.join(OUT, "thermalith.png"))
master.resize((256, 256), Image.LANCZOS).save(os.path.join(OUT, "thermalith-256.png"))

# Multi-resolution ICO for the Windows executable + window icon.
master.save(os.path.join(OUT, "thermalith.ico"),
            sizes=[(16, 16), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])

# macOS .icns for the .app bundle (assembled at packaging time, Phase 6).
try:
    master.save(os.path.join(OUT, "thermalith.icns"))
except Exception as ex:  # noqa: BLE001 - icns support is best-effort
    print("icns skipped:", ex)

print("wrote:", os.listdir(OUT))
