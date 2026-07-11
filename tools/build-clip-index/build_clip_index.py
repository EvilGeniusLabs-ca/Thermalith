#!/usr/bin/env python3
"""
Build the clip-art search index (GitHub #2) from the staged fonts + metadata.

Reads each bundled clip font's cmap (fontTools) for the present codepoints, joins
the searchable vocabulary (name + tags/keywords) from each set's companion metadata,
and emits ONE slim unified index:

    { "fonts":  [ {"key","label","count"} ... ],
      "glyphs": [ {"f":fontKey, "c":codepoint(int), "n":name, "t":[extra keywords]} ... ] }

Only this compiled index ships in the app; the raw metadata files stay as build-time
source. Regenerate after changing the font set:  python build_clip_index.py

Requires: fontTools  (pip install fonttools)
"""
import json, os, re, sys

HERE = os.path.dirname(os.path.abspath(__file__))
CLIPART = os.path.normpath(os.path.join(HERE, "..", "..", "src", "Thermalith.Core", "Fonts", "Clipart"))
META = os.path.join(CLIPART, "metadata")
OUT = os.path.join(CLIPART, "clip-index.json")

# fontFile stem, friendly tab label, tag-source strategy
FONTS = [
    ("materialdesignicons-webfont", "Material Design Icons", "mdi"),
    ("fa-solid-900",                "Font Awesome Solid",    "fa"),
    ("fa-regular-400",              "Font Awesome Regular",  "fa"),
    ("lucide",                      "Lucide",                "lucide"),
    ("tabler-icons",                "Tabler",                "tabler"),
    ("Phosphor",                    "Phosphor",              "phosphor"),
    ("NotoEmoji-VF",                "Noto Emoji",            "emoji"),
    ("NotoColorEmoji",              "Noto Color Emoji",      "emoji"),
]

SPLIT = re.compile(r"[\s/_,-]+")


def load(fn):
    return json.load(open(os.path.join(META, fn), encoding="utf-8"))


def words(*chunks):
    """Lowercased, de-duped word set from names/tags (order-stable)."""
    seen, out = set(), []
    for ch in chunks:
        for w in SPLIT.split(str(ch).lower()):
            if w and w not in seen:
                seen.add(w); out.append(w)
    return out


def tag_lookups():
    """name -> [tag words]  per tag source."""
    mdi = {e["name"]: words(*(e.get("tags", []) + e.get("aliases", []))) for e in load("mdi.meta.json")}
    fa = {k: words(*v.get("search", {}).get("terms", [])) for k, v in load("fontawesome.icons.json").items()}
    lucide = {k: words(*v) for k, v in load("lucide.tags.json").items()}
    tabler = {k: words(*v.get("tags", [])) for k, v in load("tabler.icons.json").items()}
    phosphor = {k: words(*v) for k, v in load("phosphor.tags.json").items()}
    return {"mdi": mdi, "fa": fa, "lucide": lucide, "tabler": tabler, "phosphor": phosphor}


def phosphor_codepoints():
    """name -> codepoint(int), parsed from the Phosphor stylesheet."""
    css = open(os.path.join(META, "phosphor.codepoints.css"), encoding="utf-8").read()
    pat = re.compile(r"\.ph\.ph-([a-z0-9-]+):before\s*\{\s*content:\s*\"\\([0-9a-fA-F]+)\"")
    return {name: int(hexcp, 16) for name, hexcp in pat.findall(css)}


def emoji_names():
    """codepoint(int) -> label, from emojibase (single-codepoint emoji only)."""
    out = {}
    for e in load("emoji.emojibase-en.json"):
        hexcode = e.get("hexcode", "")
        if "-" in hexcode:          # skip ZWJ / modifier sequences (multi-codepoint, not v1)
            continue
        out[int(hexcode, 16)] = e.get("label", "")
    return out


def build():
    from fontTools.ttLib import TTFont
    lookups = tag_lookups()
    ph_cp = phosphor_codepoints()
    emoji = emoji_names()

    fonts_meta, glyphs = [], []
    for stem, label, strat in FONTS:
        tf = TTFont(os.path.join(CLIPART, stem + ".ttf"), lazy=True)
        cmap = tf.getBestCmap() or {}
        count = 0

        if strat == "phosphor":
            # cmap glyph names are uniXXXX; use the stylesheet for name<->codepoint.
            for name, cp in sorted(ph_cp.items(), key=lambda kv: kv[1]):
                if cp not in cmap:
                    continue
                extra = [w for w in lookups["phosphor"].get(name, []) if w not in words(name)]
                glyphs.append({"f": stem, "c": cp, "n": name, "t": extra})
                count += 1
        elif strat == "emoji":
            for cp in sorted(cmap):
                label_txt = emoji.get(cp)
                if not label_txt:
                    continue
                glyphs.append({"f": stem, "c": cp, "n": label_txt, "t": []})
                count += 1
        else:
            table = lookups[strat]
            for cp, name in sorted(cmap.items()):
                extra = [w for w in table.get(name, []) if w not in words(name)]
                glyphs.append({"f": stem, "c": cp, "n": name, "t": extra})
                count += 1

        fonts_meta.append({"key": stem, "label": label, "count": count})
        print(f"  {label:24} {count:5} glyphs")

    index = {"fonts": fonts_meta, "glyphs": glyphs}
    json.dump(index, open(OUT, "w", encoding="utf-8"), ensure_ascii=False, separators=(",", ":"))
    size = os.path.getsize(OUT)
    print(f"\n  {len(glyphs)} glyphs across {len(fonts_meta)} fonts -> {OUT}")
    print(f"  size: {size/1024:.0f} KB")


if __name__ == "__main__":
    build()
