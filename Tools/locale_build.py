"""The language build behind Tools/build_locale.py (check / build). See that file for how to run it.

For every language in LANGUAGES it reads Tools/lang/<code>.txt, adds what CLDR (babel) knows in that language (every
country's name, the game's languages' names, the weekdays), checks placeholders and tags, and writes Assets/Resources/Lang
and the generated LanguageTable.cs.

Fonts (all SIL OFL, made into Assets/Resources/Lang/Fonts as TrueType):
- latin-*: Poppins (body) and Bebas Neue (display) plus the currency signs they lack, for languages they cover;
- lcg-*: Noto Sans (Latin, Cyrillic, Greek, Vietnamese; a condensed heavy cut for display) for the rest;
- cjk-<lang>-*: Noto Sans CJK, cut down to the characters each language uses;
- armn / geor / ethi: Noto's fonts for those scripts with Noto Sans' Latin;
- shaped scripts (Arabic, Hebrew, the Indic scripts, Thai, Lao, Khmer, Myanmar, Sinhala): Unity's text can't shape
  them, so HarfBuzz does it here. Each shaped cluster (a syllable, a joined Arabic letter with its marks) becomes one
  private-use character of a font made for the script, drawn as a composite of the original glyphs at the positions
  HarfBuzz gave them; the text is written with those characters. Right-to-left words come out already in display
  order, and the words stay in reading order for LocText to lay out line by line.
- languages: one font with every language's own name, for the language list.
"""
import os
import re
import sys
import unicodedata

import uharfbuzz as hb
from babel import Locale, UnknownLocaleError
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.cu2quPen import Cu2QuPen
from fontTools.pens.recordingPen import DecomposingRecordingPen
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import TTFont
from fontTools.ttLib.tables._g_l_y_f import Glyph, GlyphComponent
from fontTools.varLib import instancer

import build_locale as bl

ROOT = bl.ROOT
OUT_LANG = os.path.join(ROOT, "Assets", "Resources", "Lang")
OUT_FONTS = os.path.join(OUT_LANG, "Fonts")
OUT_TABLE = os.path.join(ROOT, "Assets", "Scripts", "Game", "Locale", "LanguageTable.cs")
NOTO = "/usr/share/fonts/noto"
CJK = "/usr/share/fonts/noto-cjk"
GAME_FONTS = os.path.join(ROOT, "Assets", "Resources", "Fonts")
NOTO_VF = os.path.join(ROOT, "Tools", ".external", "fonts_dl", "NotoSans-VF.ttf")

# code, English name, CLDR locale, script group, right-to-left, device languages that pick it (lower case)
LANGUAGES = [
    ("en", "English", "en", "latin", False, ["en"]),
    ("af", "Afrikaans", "af", "latin", False, ["af"]),
    ("am", "Amharic", "am", "ethi", False, ["am"]),
    ("ar", "Arabic", "ar", "arab", True, ["ar"]),
    ("az", "Azerbaijani", "az", "latin", False, ["az"]),
    ("be", "Belarusian", "be", "lcg", False, ["be"]),
    ("bg", "Bulgarian", "bg", "lcg", False, ["bg"]),
    ("bn", "Bengali", "bn", "beng", False, ["bn"]),
    ("bs", "Bosnian", "bs", "latin", False, ["bs"]),
    ("ca", "Catalan", "ca", "latin", False, ["ca"]),
    ("cs", "Czech", "cs", "latin", False, ["cs"]),
    ("da", "Danish", "da", "latin", False, ["da"]),
    ("de", "German", "de", "latin", False, ["de"]),
    ("el", "Greek", "el", "lcg", False, ["el"]),
    ("es", "Spanish", "es", "latin", False, ["es"]),
    ("et", "Estonian", "et", "latin", False, ["et"]),
    ("eu", "Basque", "eu", "latin", False, ["eu"]),
    ("fa", "Persian", "fa", "arab", True, ["fa"]),
    ("fi", "Finnish", "fi", "latin", False, ["fi"]),
    ("fil", "Filipino", "fil", "latin", False, ["fil"]),
    ("fr", "French", "fr", "latin", False, ["fr"]),
    ("ga", "Irish", "ga", "latin", False, ["ga"]),
    ("gl", "Galician", "gl", "latin", False, ["gl"]),
    ("gu", "Gujarati", "gu", "gujr", False, ["gu"]),
    ("ha", "Hausa", "ha", "latin", False, ["ha"]),
    ("he", "Hebrew", "he", "hebr", True, ["he"]),
    ("hi", "Hindi", "hi", "deva", False, ["hi"]),
    ("hr", "Croatian", "hr", "latin", False, ["hr"]),
    ("ht", "Haitian Creole", "fr_HT", "latin", False, ["ht"]),
    ("hu", "Hungarian", "hu", "latin", False, ["hu"]),
    ("hy", "Armenian", "hy", "armn", False, ["hy"]),
    ("id", "Indonesian", "id", "latin", False, ["id"]),
    ("ig", "Igbo", "ig", "latin", False, ["ig"]),
    ("is", "Icelandic", "is", "latin", False, ["is"]),
    ("it", "Italian", "it", "latin", False, ["it"]),
    ("ja", "Japanese", "ja", "cjk-ja", False, ["ja"]),
    ("ka", "Georgian", "ka", "geor", False, ["ka"]),
    ("kk", "Kazakh", "kk", "lcg", False, ["kk"]),
    ("km", "Khmer", "km", "khmr", False, ["km"]),
    ("kn", "Kannada", "kn", "knda", False, ["kn"]),
    ("ko", "Korean", "ko", "cjk-ko", False, ["ko"]),
    ("ky", "Kyrgyz", "ky", "lcg", False, ["ky"]),
    ("lb", "Luxembourgish", "lb", "latin", False, ["lb"]),
    ("lo", "Lao", "lo", "laoo", False, ["lo"]),
    ("lt", "Lithuanian", "lt", "latin", False, ["lt"]),
    ("lv", "Latvian", "lv", "latin", False, ["lv"]),
    ("mg", "Malagasy", "mg", "latin", False, ["mg"]),
    ("mk", "Macedonian", "mk", "lcg", False, ["mk"]),
    ("ml", "Malayalam", "ml", "mlym", False, ["ml"]),
    ("mn", "Mongolian", "mn", "lcg", False, ["mn"]),
    ("mr", "Marathi", "mr", "deva", False, ["mr"]),
    ("ms", "Malay", "ms", "latin", False, ["ms"]),
    ("mt", "Maltese", "mt", "latin", False, ["mt"]),
    ("my", "Burmese", "my", "mymr", False, ["my"]),
    ("nb", "Norwegian", "nb", "latin", False, ["nb"]),
    ("ne", "Nepali", "ne", "deva", False, ["ne"]),
    ("nl", "Dutch", "nl", "latin", False, ["nl"]),
    ("pa", "Punjabi", "pa", "guru", False, ["pa"]),
    ("pl", "Polish", "pl", "latin", False, ["pl"]),
    ("ps", "Pashto", "ps", "arab", True, ["ps"]),
    ("pt-BR", "Portuguese (Brazil)", "pt_BR", "latin", False, ["pt-br", "pt"]),
    ("pt-PT", "Portuguese (Portugal)", "pt_PT", "latin", False,
     ["pt-pt", "pt-ao", "pt-mz", "pt-cv", "pt-gw", "pt-st", "pt-tl", "pt-mo", "pt-gq", "pt-lu", "pt-ch"]),
    ("rm", "Romansh", "rm", "latin", False, ["rm"]),
    ("ro", "Romanian", "ro", "latin", False, ["ro", "mo"]),
    ("ru", "Russian", "ru", "lcg", False, ["ru"]),
    ("rw", "Kinyarwanda", "rw", "latin", False, ["rw"]),
    ("si", "Sinhala", "si", "sinh", False, ["si"]),
    ("sk", "Slovak", "sk", "latin", False, ["sk"]),
    ("sl", "Slovenian", "sl", "latin", False, ["sl"]),
    ("so", "Somali", "so", "latin", False, ["so"]),
    ("sq", "Albanian", "sq", "latin", False, ["sq"]),
    ("sr", "Serbian", "sr", "lcg", False, ["sr"]),
    ("sv", "Swedish", "sv", "latin", False, ["sv"]),
    ("sw", "Swahili", "sw", "latin", False, ["sw"]),
    ("ta", "Tamil", "ta", "taml", False, ["ta"]),
    ("te", "Telugu", "te", "telu", False, ["te"]),
    ("tg", "Tajik", "tg", "lcg", False, ["tg"]),
    ("th", "Thai", "th", "thai", False, ["th"]),
    ("tk", "Turkmen", "tk", "latin", False, ["tk"]),
    ("tr", "Turkish", "tr", "latin", False, ["tr"]),
    ("uk", "Ukrainian", "uk", "lcg", False, ["uk"]),
    ("ur", "Urdu", "ur", "arab", True, ["ur"]),
    ("uz", "Uzbek", "uz", "latin", False, ["uz"]),
    ("vi", "Vietnamese", "vi", "lcg", False, ["vi"]),
    ("xh", "Xhosa", "xh", "latin", False, ["xh"]),
    ("yo", "Yoruba", "yo", "latin", False, ["yo"]),
    ("zh-Hans", "Chinese (Simplified)", "zh_Hans", "cjk-sc", False, ["zh-hans"]),
    ("zh-Hant", "Chinese (Traditional)", "zh_Hant", "cjk-tc", False, ["zh-hant"]),
    ("zu", "Zulu", "zu", "latin", False, ["zu"]),
]

SHAPED = {
    "arab": "Arabic", "hebr": "Hebrew", "deva": "Devanagari", "beng": "Bengali", "gujr": "Gujarati", "guru": "Gurmukhi",
    "taml": "Tamil", "telu": "Telugu", "knda": "Kannada", "mlym": "Malayalam", "sinh": "Sinhala", "thai": "Thai",
    "laoo": "Lao", "khmr": "Khmer", "mymr": "Myanmar",
}
SIMPLE_SCRIPTS = {"armn": "Armenian", "geor": "Georgian", "ethi": "Ethiopic"}
CJK_FACES = {"cjk-ja": 0, "cjk-ko": 1, "cjk-sc": 2, "cjk-tc": 3}
KINDS = ["Display", "Body", "Semi", "Bold"]

# In every font: ASCII, the signs prices use, and the punctuation the game's English uses.
BASE_CHARS = set(chr(c) for c in range(0x20, 0x7F)) | set(
    " €$£¥¢₹₩₺₽₴₪₫₱฿₦₵₸₼₾₲₡₭₮₨•×–—“”‘’«»‹›…÷°±·„‚") | set("¡¿")
PUA_START = 0xE000
MIRROR = {"(": ")", ")": "(", "[": "]", "]": "[", "{": "}", "}": "{", "<": ">", ">": "<", "«": "»", "»": "«", "‹": "›", "›": "‹"}
TAG = re.compile(r"<[^<>]+>")
PLACEHOLDER = re.compile(r"\{\d+(?::[^{}]*)?\}")


# ------------------------------------------------------------------------------------------------ fonts in


class Source:
    """A font to take glyphs from (a variable font is cut at the given axes), and to shape with."""
    cache = {}

    def __init__(self, path, index=0, axes=None):
        self.path, self.index = path, index
        tt = TTFont(path, fontNumber=index, lazy=False)
        if axes:
            tt = instancer.instantiateVariableFont(tt, axes)
        self.tt = tt
        self.cmap = tt.getBestCmap()
        self.gs = tt.getGlyphSet()
        self.cff = "CFF " in tt or "CFF2" in tt
        self.upem = tt["head"].unitsPerEm
        self.order = tt.getGlyphOrder()
        self._hb = None
        self.axes = axes

    @classmethod
    def get(cls, path, index=0, axes=None):
        key = (path, index, tuple(sorted((axes or {}).items())))
        if key not in cls.cache:
            cls.cache[key] = Source(path, index, axes)
        return cls.cache[key]

    @property
    def hb(self):
        if self._hb is None:
            if self.axes:
                import io
                buf = io.BytesIO()
                self.tt.save(buf)
                face = hb.Face(hb.Blob(buf.getvalue()))
            else:
                face = hb.Face(hb.Blob.from_file_path(self.path), self.index)
            self._hb = hb.Font(face)
        return self._hb

    def has(self, ch):
        return ord(ch) in self.cmap

    def advance(self, gname):
        return self.tt["hmtx"][gname][0]

    def draw(self, gname, pen):
        rec = DecomposingRecordingPen(self.gs)
        self.gs[gname].draw(rec)
        rec.replay(Cu2QuPen(pen, 1.0, reverse_direction=True) if self.cff else pen)


def noto(script, weight):
    return Source.get(os.path.join(NOTO, f"NotoSans{script}-{weight}.ttf"))


def noto_latin(weight):
    wght = {"Regular": 400, "SemiBold": 600, "Bold": 700, "Display": 800}[weight]
    axes = {"wght": wght, "wdth": 70.0 if weight == "Display" else 100.0}
    return Source.get(NOTO_VF, 0, axes)


def cjk(group, weight):
    return Source.get(os.path.join(CJK, f"NotoSansCJK-{weight}.ttc"), CJK_FACES[group])


# ------------------------------------------------------------------------------------------------ fonts out


class FontMaker:
    """A TrueType font put together from glyphs of other fonts, and private-use clusters made of them."""

    def __init__(self, family, style):
        self.family, self.style = family, style
        self.glyphs = {".notdef": self._empty(), "space": self._empty()}
        self.advances = {".notdef": 500, "space": 260}
        self.order = [".notdef", "space"]
        self.cmap = {0x20: "space"}
        self.copied = {}

    @staticmethod
    def _empty():
        g = Glyph()
        g.numberOfContours = 0
        return g

    def _new(self, glyph, advance):
        name = f"g{len(self.order)}"
        self.order.append(name)
        self.glyphs[name] = glyph
        self.advances[name] = advance
        return name

    def copy(self, src, gname):
        """The new font's copy of a source glyph (made once)."""
        key = (id(src), gname)
        if key not in self.copied:
            pen = TTGlyphPen(None)
            src.draw(gname, pen)
            glyph = pen.glyph()
            scale = 1000 / src.upem
            if scale != 1:
                glyph.coordinates.scale((scale, scale))
            self.copied[key] = self._new(glyph, round(src.advance(gname) * scale))
        return self.copied[key]

    def add_char(self, ch, sources):
        cp = ord(ch)
        if cp in self.cmap:
            return True
        if ch == " ":
            self.cmap[cp] = "space"
            return True
        for src in sources:
            if src is not None and cp in src.cmap:
                self.cmap[cp] = self.copy(src, src.cmap[cp])
                return True
        return False

    def add_cluster(self, cp, src, components, advance):
        """A private-use character drawn as glyphs of src at (dx, dy) from its pen position."""
        g = Glyph()
        g.numberOfContours = -1
        g.components = []
        for gname, dx, dy in components:
            c = GlyphComponent()
            c.glyphName = self.copy(src, gname)
            c.x, c.y = int(round(dx)), int(round(dy))
            c.flags = 0x0004   # ROUND_XY_TO_GRID
            g.components.append(c)
        if not g.components:
            g = self._empty()
        self.cmap[cp] = self._new(g, max(0, int(round(advance))))

    def save(self, path, ascent, descent, line_gap=0):
        fb = FontBuilder(1000, isTTF=True)
        fb.setupGlyphOrder(self.order)
        fb.setupCharacterMap(self.cmap)
        fb.setupGlyf(self.glyphs)
        glyf = fb.font["glyf"]
        metrics = {}
        for name in self.order:
            g = glyf[name]
            g.recalcBounds(glyf)
            metrics[name] = (self.advances[name], getattr(g, "xMin", 0) if g.numberOfContours != 0 else 0)
        fb.setupHorizontalMetrics(metrics)
        fb.setupHorizontalHeader(ascent=ascent, descent=-abs(descent), lineGap=line_gap)
        fb.setupNameTable({"familyName": self.family, "styleName": self.style,
                           "copyright": "Made from fonts licensed under the SIL Open Font License 1.1 (Noto, Poppins, Bebas Neue)"})
        fb.setupOS2(sTypoAscender=ascent, sTypoDescender=-abs(descent), sTypoLineGap=line_gap, usWinAscent=ascent,
                    usWinDescent=abs(descent), achVendID="LUCK", fsType=0)
        fb.setupPost()
        fb.save(path)
        return os.path.getsize(path)


# ------------------------------------------------------------------------------------------------ shaping


def bidi_class(ch):
    return unicodedata.bidirectional(ch) or "ON"


class Shaper:
    """Turns text of one shaped script into private-use characters of the font made for it."""

    def __init__(self, group, regular, bold, start=PUA_START):
        self.group, self.regular, self.bold = group, regular, bold
        self.rtl = group in ("arab", "hebr")
        self.next = start
        self.codes = {}          # cluster key -> code point
        self.reg_parts = {}      # code point -> (components, advance) in the regular font
        self.bold_parts = {}     # code point -> (components, advance) in the bold font

    def shapeable(self, ch):
        if ch in "  \n":
            return False
        if ord(ch) < 0x80:
            return False
        cls = bidi_class(ch)
        if self.regular.has(ch):
            return True
        # Marks and joiners belong with the letters around them even if the font has no glyph of their own.
        return cls in ("NSM", "BN") or ch in "‌‍"

    @staticmethod
    def _clusters(src, text, rtl):
        buf = hb.Buffer()
        buf.add_str(text)
        buf.guess_segment_properties()
        buf.direction = "rtl" if rtl else "ltr"
        hb.shape(src.hb, buf, {})
        out, last = [], None
        for info, pos in zip(buf.glyph_infos, buf.glyph_positions):
            name = src.order[info.codepoint]
            if info.cluster != last:
                out.append([info.cluster, [], 0])
                last = info.cluster
            c = out[-1]
            if not (info.codepoint == 0 and pos.x_advance == 0):
                c[1].append((name, c[2] + pos.x_offset, pos.y_offset))
            c[2] += pos.x_advance
        return out

    def encode_run(self, text):
        """Display-order private-use characters for a run of the script (logical order in)."""
        reg = self._clusters(self.regular, text, self.rtl)
        bold = self._clusters(self.bold, text, self.rtl)
        same = [c[0] for c in reg] == [c[0] for c in bold]
        out = []
        for i, (cluster, comps, adv) in enumerate(reg):
            key = (tuple(comps), adv)
            cp = self.codes.get(key)
            if cp is None:
                cp = self.next
                self.next += 1
                if self.next > 0xF8FF:
                    raise SystemExit(f"{self.group}: out of private-use characters")
                self.codes[key] = cp
                self.reg_parts[cp] = (comps, adv)
            if cp not in self.bold_parts and same:
                self.bold_parts[cp] = (bold[i][1], bold[i][2])
            out.append(chr(cp))
        return "".join(out)

    def encode(self, text):
        """A whole string: tags and placeholders kept, words separated by spaces, each word encoded."""
        lines = []
        for line in text.split("\n"):
            words = []
            for word in line.split(" "):
                words.append(self.encode_word(word) if word else "")
            lines.append(" ".join(words))
        return "\n".join(lines)

    def encode_word(self, word):
        # Tags go round the word (LocText re-wraps them on every word anyway).
        opening = "".join(t for t in TAG.findall(word) if not t.startswith("</"))
        closing = "".join(t for t in TAG.findall(word) if t.startswith("</"))
        core = TAG.sub("", word)
        tokens = []   # (kind, text): 's' shapeable, 'p' placeholder, 'l' literal char
        i = 0
        while i < len(core):
            m = PLACEHOLDER.match(core, i)
            if m:
                tokens.append(("p", m.group(0)))
                i = m.end()
                continue
            ch = core[i]
            kind = "s" if self.shapeable(ch) else "l"
            if kind == "s" and tokens and tokens[-1][0] == "s":
                tokens[-1] = ("s", tokens[-1][1] + ch)
            else:
                tokens.append((kind, ch))
            i += 1
        if not self.rtl:
            body = "".join(self.encode_run(t) if k == "s" else t for k, t in tokens)
            return opening + body + closing
        # A value put in at run time can be several words (a country, a sentence): it goes in a piece of its own,
        # glued to the word round it with U+200B, so LocText can lay its words out right to left with the rest.
        # Number signs stay with it (100%, +300, 3/10).
        def numeric(k, t):
            return k == "p" or (k == "l" and (bidi_class(t) in ("EN", "ET", "ES", "CS", "AN") or t in "+-\u2212\u00d7%"))
        runs = []
        for k, t in tokens:
            n = numeric(k, t)
            if runs and runs[-1][0] == n:
                runs[-1][1].append((k, t))
            else:
                runs.append([n, [(k, t)]])
        pieces, buf = [], []
        for n, run in runs:
            if n and any(k == "p" for k, _ in run):
                lead = []
                while run and run[0][0] == "l" and bidi_class(run[0][1]) == "CS":
                    lead.append(run.pop(0))
                trail = []
                while run and run[-1][0] == "l" and bidi_class(run[-1][1]) == "CS":
                    trail.insert(0, run.pop())
                buf += lead
                if buf:
                    pieces.append(buf)
                pieces.append(run)
                buf = trail
            else:
                buf += run
        if buf:
            pieces.append(buf)
        return opening + "\u200b".join(self._rtl_word(piece) for piece in pieces) + closing

    def _rtl_word(self, tokens):
        """One word of right-to-left text in display order (a small part of the Unicode bidi algorithm)."""
        def strong(k, t):
            if k == "s":
                if any(bidi_class(c) in ("R", "AL") for c in t):
                    return "R"
                return "N" if all(bidi_class(c) in ("AN", "EN") for c in t) else None   # Arabic comma: neutral
            if k == "p":
                return "N"   # a number goes in: like a European number, it makes neutrals around it right-to-left
            cls = bidi_class(t)
            if cls == "L":
                return "L"
            if cls in ("EN", "AN"):
                return "N"
            return None
        types = [strong(k, t) for k, t in tokens]
        # Numbers with their separators stay together, left to right (1,000 / 3.5 / 1-18), and so do the signs that go
        # with a number (100%, +5, $3).
        for i, (k, t) in enumerate(tokens):
            if types[i] is not None or k != "l":
                continue
            prev = types[i - 1] if i > 0 else None
            nxt = types[i + 1] if i + 1 < len(tokens) else None
            if t in ",.:/-+%" and prev in ("N", "L") and nxt in ("N", "L"):
                types[i] = prev
            elif bidi_class(t) == "ET" or t in "+−":
                if prev == "N" or nxt == "N":
                    types[i] = "N"
        levels = []
        for i, t in enumerate(types):
            if t == "L" or t == "N":
                levels.append("L")
            elif t == "R":
                levels.append("R")
            else:
                prev = next((types[j] for j in range(i - 1, -1, -1) if types[j]), None)
                nxt = next((types[j] for j in range(i + 1, len(types)) if types[j]), None)
                levels.append("L" if prev == "L" and nxt == "L" else "R")
        # Runs of the same direction; the word's runs read right to left.
        runs = []
        for (k, t), lv in zip(tokens, levels):
            if runs and runs[-1][0] == lv:
                runs[-1][1].append((k, t))
            else:
                runs.append([lv, [(k, t)]])
        out = []
        for lv, items in reversed(runs):
            if lv == "L":
                out.append("".join(t if k != "s" else self.encode_run(t) for k, t in items))
            else:
                parts = []
                for k, t in reversed(items):
                    parts.append(self.encode_run(t) if k == "s" else "".join(MIRROR.get(c, c) for c in reversed(t)))
                out.append("".join(parts))
        return "".join(out)


# ------------------------------------------------------------------------------------------------ CLDR


def cldr(lang):
    try:
        return Locale.parse(lang[2])
    except (UnknownLocaleError, ValueError):
        return Locale.parse("en")


def capital(s):
    if not s or "\u10d0" <= s[0] <= "\u10ff":   # Georgian Mkhedruli is written without capitals
        return s
    return s[:1].upper() + s[1:]


def language_name(code_locale, in_locale):
    try:
        return capital(Locale.parse(code_locale).get_display_name(in_locale) or "")
    except (UnknownLocaleError, ValueError):
        return ""


def extras(lang, countries, all_langs):
    """Keys and texts CLDR knows in this language: country, language and weekday names."""
    loc = cldr(lang)
    en = Locale.parse("en")
    out = {}
    for cc in countries:
        out["@country:" + cc] = loc.territories.get(cc) or en.territories.get(cc) or cc
    for other in all_langs:
        name = language_name(other[2], loc) if lang[0] != "ht" else ""
        if not name and lang[0] == "ht":
            name = language_name(other[2], Locale.parse("fr"))
        out["@language:" + other[0]] = name or other[1]
    days = loc.days["format"]["abbreviated"]
    for dow in range(7):
        out[f"@weekday:{dow}"] = capital(days[(dow + 6) % 7])
    if lang[0] == "ht":
        out.update(HAITIAN_DAYS)
    return out


HAITIAN_DAYS = {"@weekday:0": "Dim", "@weekday:1": "Len", "@weekday:2": "Mad", "@weekday:3": "Mèk", "@weekday:4": "Jed",
                "@weekday:5": "Van", "@weekday:6": "Sam"}


def number_style(lang):
    loc = cldr(lang)
    symbols = loc.number_symbols
    sym = symbols.get("latn", symbols) if isinstance(symbols, dict) else symbols
    decimal, group = sym.get("decimal", "."), sym.get("group", ",")
    group = group.replace(" ", " ").replace("’", "'")
    fmt = loc.decimal_formats.get(None)
    primary, secondary = fmt.grouping if fmt is not None else (3, 3)
    cur = loc.currency_formats.get("standard")
    pattern = cur.pattern if cur is not None else "¤#,##0.00"
    pattern = pattern.split(";")[0].replace("‎", "").replace("‏", "").replace("؜", "")
    first = pattern.strip().startswith("¤")
    space = ("¤ " in pattern or "¤ " in pattern) if first else (" ¤" in pattern or " ¤" in pattern)
    return decimal, group, primary, secondary, first, space


# ------------------------------------------------------------------------------------------------ the build


def check_line(english, text):
    problems = []
    if sorted(PLACEHOLDER.findall(english)) != sorted(PLACEHOLDER.findall(text)):
        problems.append("placeholders differ")
    if sorted(TAG.findall(english)) != sorted(TAG.findall(text)):
        problems.append("tags differ")
    return problems


def typography(code, text):
    """Non-breaking spaces where a line must not break: before %, and French spacing around : ; ! ? « »."""
    text = text.replace(" %", "\u00a0%")
    if code == "fr":
        text = re.sub(r" (?=[:;!?»])", "\u00a0", text).replace("« ", "«\u00a0")
    return text


def load_translations(found):
    """{code: {english: text}} and the problems found."""
    ids = {bl.phrase_id(e): e for e, _ in found}
    result, report = {}, []
    for lang in LANGUAGES:
        code = lang[0]
        if code == "en":
            result[code] = {}
            continue
        path = os.path.join(bl.LANG_DIR, code + ".txt")
        if not os.path.exists(path):
            report.append(f"{code}: no translation file")
            result[code] = {}
            continue
        entries = bl.read_lang(path)
        table, missing, stale, broken = {}, 0, 0, 0
        for pid, text in entries.items():
            english = ids.get(pid)
            if english is None:
                stale += 1
                continue
            if check_line(english, text):
                broken += 1
                report.append(f"{code} {pid}: {', '.join(check_line(english, text))}: {text[:70]}")
                continue
            table[english] = typography(code, text)
        missing = len(ids) - len(table) - broken
        if missing or stale:
            report.append(f"{code}: {len(table)}/{len(ids)} translated, {missing} missing, {stale} stale")
        result[code] = table
    return result, report


def run(command, found):
    translations, report = load_translations(found)
    for r in report:
        print("CHECK", r)
    if command != "build":
        return
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    countries = country_codes()
    english = [e for e, _ in found]
    extra_keys = [f"@country:{c}" for c in countries] + [f"@language:{l[0]}" for l in LANGUAGES] + [f"@weekday:{d}" for d in range(7)]
    keys = english + extra_keys
    os.makedirs(OUT_FONTS, exist_ok=True)

    texts = {}   # code -> list of plain texts (aligned with keys; "" = no translation)
    for lang in LANGUAGES:
        code = lang[0]
        ex = extras(lang, countries, LANGUAGES)
        table = translations.get(code, {})
        texts[code] = [table.get(e, "") for e in english] + [ex[k] for k in extra_keys]

    fonts, encoded, sizes = {}, {}, []
    groups = {}
    for lang in LANGUAGES:
        groups.setdefault(lang[3], []).append(lang)

    # Latin languages Poppins and Bebas Neue cover keep them; the rest go to Noto Sans.
    latin_sources = {k: Source.get(os.path.join(GAME_FONTS, f)) for k, f in
                     (("Display", "BebasNeue-Regular.ttf"), ("Body", "Poppins-Regular.ttf"), ("Semi", "Poppins-SemiBold.ttf"),
                      ("Bold", "Poppins-Bold.ttf"))}
    lcg_weights = {"Display": "Display", "Body": "Regular", "Semi": "SemiBold", "Bold": "Bold"}
    lcg_need = {k: set(BASE_CHARS) for k in KINDS}
    latin_need = {k: set(BASE_CHARS) for k in KINDS}
    for lang in groups.get("latin", []) + groups.get("lcg", []):
        chars = set("".join(texts[lang[0]])) - {"\n"}
        chars |= set("".join(c.upper() for c in chars))
        per_kind = []
        for k in KINDS:
            src = latin_sources[k]
            covered = lang[3] == "latin" and all(src.has(c) or c in BASE_CHARS for c in chars)
            per_kind.append("latin" if covered else "lcg")
            (latin_need if covered else lcg_need)[k] |= chars
        fonts[lang[0]] = [f"Lang/Fonts/{g}-{k.lower()}" for g, k in zip(per_kind, KINDS)]
        encoded[lang[0]] = texts[lang[0]]
    symbols = [noto_latin("Bold"), noto_latin("Regular"), noto("Thai", "Regular")]
    for k in KINDS:
        fm = FontMaker("Lucky Latin", k)
        for c in sorted(latin_need[k]):
            fm.add_char(c, [latin_sources[k], noto_latin(lcg_weights[k])] + symbols)
        asc, desc = (900, 300) if k == "Display" else (1050, 350)
        sizes.append(("latin-" + k.lower(), fm.save(os.path.join(OUT_FONTS, f"latin-{k.lower()}.ttf"), asc, desc)))
        fm = FontMaker("Lucky Noto", k)
        for c in sorted(lcg_need[k]):
            fm.add_char(c, [noto_latin(lcg_weights[k]), noto_latin("Regular")] + symbols)
        sizes.append(("lcg-" + k.lower(), fm.save(os.path.join(OUT_FONTS, f"lcg-{k.lower()}.ttf"), asc, desc)))

    # CJK: each language's own characters.
    for group in CJK_FACES:
        for lang in groups.get(group, []):
            chars = set("".join(texts[lang[0]])) | BASE_CHARS
            for weight, style in (("Regular", "regular"), ("Bold", "bold")):
                fm = FontMaker("Lucky CJK " + lang[0], style)
                src = cjk(group, weight)
                for c in sorted(chars - {"\n"}):
                    fm.add_char(c, [src, noto_latin("Bold" if weight == "Bold" else "Regular")] + symbols)
                name = f"cjk-{lang[0].lower()}-{style}"
                sizes.append((name, fm.save(os.path.join(OUT_FONTS, name + ".ttf"), 1080, 320)))
            fonts[lang[0]] = [f"Lang/Fonts/cjk-{lang[0].lower()}-bold", f"Lang/Fonts/cjk-{lang[0].lower()}-regular",
                              f"Lang/Fonts/cjk-{lang[0].lower()}-bold", f"Lang/Fonts/cjk-{lang[0].lower()}-bold"]
            encoded[lang[0]] = texts[lang[0]]

    # Scripts with their own Noto font and no shaping (Armenian, Georgian, Ethiopic).
    for group, script in SIMPLE_SCRIPTS.items():
        langs = groups.get(group, [])
        if not langs:
            continue
        chars = set("".join("".join(texts[l[0]]) for l in langs)) | BASE_CHARS
        chars |= set("".join(c.upper() for c in chars))
        for weight, style in (("Regular", "regular"), ("Bold", "bold")):
            fm = FontMaker("Lucky " + script, style)
            for c in sorted(chars - {"\n"}):
                fm.add_char(c, [noto(script, weight), noto_latin("Bold" if weight == "Bold" else "Regular")] + symbols)
            sizes.append((f"{group}-{style}", fm.save(os.path.join(OUT_FONTS, f"{group}-{style}.ttf"), 1100, 350)))
        for l in langs:
            fonts[l[0]] = [f"Lang/Fonts/{group}-bold", f"Lang/Fonts/{group}-regular", f"Lang/Fonts/{group}-bold", f"Lang/Fonts/{group}-bold"]
            encoded[l[0]] = texts[l[0]]

    # Shaped scripts.
    plain = {}
    for group, script in SHAPED.items():
        langs = groups.get(group, [])
        if not langs:
            continue
        shaper = Shaper(group, noto(script, "Regular"), noto(script, "Bold"))
        latin_chars = set(BASE_CHARS)
        for l in langs:
            enc = []
            for t in texts[l[0]]:
                enc.append(shaper.encode(t) if t else "")
                latin_chars |= {c for c in t if not shaper.shapeable(c) and c != "\n"}
            encoded[l[0]] = enc
            plain[l[0]] = texts[l[0]]
        src_ascent = shaper.regular.tt["hhea"].ascent
        src_descent = abs(shaper.regular.tt["hhea"].descent)
        asc, desc = max(1050, int(src_ascent * 0.92)), max(350, int(src_descent * 0.92))
        for weight, style, parts, src in (("Regular", "regular", shaper.reg_parts, shaper.regular),
                                          ("Bold", "bold", shaper.bold_parts, shaper.bold)):
            fm = FontMaker("Lucky " + script, style)
            for c in sorted(latin_chars):
                fm.add_char(c, [noto_latin("Bold" if weight == "Bold" else "Regular")] + symbols)
            for cp in sorted(shaper.reg_parts):
                if cp in parts:
                    comps, adv = parts[cp]
                    fm.add_cluster(cp, src, comps, adv)
                else:
                    comps, adv = shaper.reg_parts[cp]
                    fm.add_cluster(cp, shaper.regular, comps, adv)
            sizes.append((f"{group}-{style}", fm.save(os.path.join(OUT_FONTS, f"{group}-{style}.ttf"), asc, desc)))
        print(f"  {group}: {len(shaper.reg_parts)} shaped clusters for {', '.join(l[0] for l in langs)}")
        for l in langs:
            fonts[l[0]] = [f"Lang/Fonts/{group}-bold", f"Lang/Fonts/{group}-regular", f"Lang/Fonts/{group}-bold", f"Lang/Fonts/{group}-bold"]

    # The language list: every language's own name in one font.
    native, native_encoded = {}, {}
    fm = FontMaker("Lucky Languages", "Regular")
    for c in sorted(BASE_CHARS):
        fm.add_char(c, [noto_latin("SemiBold")] + symbols)
    shapers = {}
    next_cp = PUA_START
    for lang in LANGUAGES:
        name = native_name(lang)
        native[lang[0]] = name
        group = lang[3]
        if group in SHAPED:
            sh = shapers.get(group)
            if sh is None:
                sh = shapers[group] = Shaper(group, noto(SHAPED[group], "Medium"), noto(SHAPED[group], "Medium"), next_cp)
            native_encoded[lang[0]] = sh.encode(name)
            next_cp = max(next_cp, sh.next)
            for s in shapers.values():
                s.next = max(s.next, next_cp)
        else:
            native_encoded[lang[0]] = name
            sources = [noto_latin("SemiBold")]
            if group.startswith("cjk"):
                sources.append(cjk(group, "Medium"))
            if group in SIMPLE_SCRIPTS:
                sources.append(noto(SIMPLE_SCRIPTS[group], "Medium"))
            for c in name:
                if not fm.add_char(c, sources + symbols):
                    print(f"  languages font: no glyph for {c!r} in {lang[0]}")
    for sh in shapers.values():
        for cp, (comps, adv) in sh.reg_parts.items():
            fm.add_cluster(cp, sh.regular, comps, adv)
    sizes.append(("languages", fm.save(os.path.join(OUT_FONTS, "languages.ttf"), 1150, 450)))

    write_lang_files(keys, encoded, plain)
    write_table(fonts, native_encoded)
    copy_licences()
    total = sum(s for _, s in sizes)
    print(f"{len(sizes)} fonts, {total / 1e6:.1f} MB: " + ", ".join(f"{n} {s // 1024}K" for n, s in sizes))


def native_name(lang):
    if lang[0] == "ht":
        return "Kreyòl ayisyen"
    loc = cldr(lang)
    return language_name(lang[2], loc) or lang[1]


def country_codes():
    import pycountry
    return sorted({c.alpha_2 for c in pycountry.countries} | {"XK"})


def write_lang_files(keys, encoded, plain):
    os.makedirs(OUT_LANG, exist_ok=True)
    with open(os.path.join(OUT_LANG, "keys.txt"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(bl.escape(k) for k in keys))
    for code, lines in encoded.items():
        assert len(lines) == len(keys), code
        with open(os.path.join(OUT_LANG, code + ".txt"), "w", encoding="utf-8", newline="\n") as f:
            f.write("\n".join(bl.escape(t) for t in lines))
    for code, lines in plain.items():
        with open(os.path.join(OUT_LANG, code + ".plain.txt"), "w", encoding="utf-8", newline="\n") as f:
            f.write("\n".join(bl.escape(t) for t in lines))
    # Languages no longer built: remove their files.
    built = set(encoded) | {c + ".plain" for c in plain} | {"keys"}
    for name in os.listdir(OUT_LANG):
        if name.endswith(".txt") and name[:-4] not in built:
            os.remove(os.path.join(OUT_LANG, name))
            meta = os.path.join(OUT_LANG, name + ".meta")
            if os.path.exists(meta):
                os.remove(meta)


def cs_string(s):
    out = []
    for ch in s:
        if ch in '"\\':
            out.append("\\" + ch)
        elif ord(ch) < 0x20 or ord(ch) > 0x7E:
            out.append(f"\\u{ord(ch):04X}" if ord(ch) <= 0xFFFF else ch)
        else:
            out.append(ch)
    return '"' + "".join(out) + '"'


def write_table(fonts, native_encoded):
    rows = []
    for lang in LANGUAGES:
        code, english, loc, group, rtl, matches = lang
        decimal, grp, primary, secondary, first, space = number_style(lang)
        f = fonts.get(code, [None] * 4)
        font_list = ", ".join(cs_string(x) if x else "null" for x in f)
        rows.append(
            f'            new LanguageInfo {{ Code = {cs_string(code)}, EnglishName = {cs_string(english)}, '
            f'NativeName = {cs_string(native_encoded[code])}, RightToLeft = {"true" if rtl else "false"},\n'
            f'                Fonts = new string[] {{ {font_list} }},\n'
            f'                Decimal = {cs_string(decimal)}, Group = {cs_string(grp)}, PrimaryGroup = {primary}, SecondaryGroup = {secondary}, '
            f'CurrencyFirst = {"true" if first else "false"}, CurrencySpace = {"true" if space else "false"},\n'
            f'                Matches = new[] {{ {", ".join(cs_string(m) for m in matches)} }} }},')
    src = """// <auto-generated>
// Generated by Tools/build_locale.py - do not edit by hand, re-run the script instead.
// Number and price formats from CLDR (babel); fonts made by Tools/locale_build.py.
// </auto-generated>
namespace Casino
{
    public static class LanguageTable
    {
        public static readonly LanguageInfo[] All =
        {
""" + "\n".join(rows) + """
        };

        public static readonly LanguageInfo English = All[0];
    }
}
"""
    open(OUT_TABLE, "w", encoding="utf-8", newline="\n").write(src)


def copy_licences():
    import shutil
    src = os.path.join(ROOT, "Tools", ".external", "fonts_dl", "OFL-NotoSans.txt")
    if os.path.exists(src):
        shutil.copyfile(src, os.path.join(OUT_FONTS, "LICENSE-Noto-OFL.txt"))
    for name in ("LICENSE-Poppins-OFL.txt", "LICENSE-BebasNeue-OFL.txt"):
        shutil.copyfile(os.path.join(GAME_FONTS, name), os.path.join(OUT_FONTS, name))
