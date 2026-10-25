#!/usr/bin/env python3
"""Builds the game's languages.

    Tools/.external/pyenv/bin/python Tools/build_locale.py [extract|check|build]

extract  Collects the English the code shows players (Loc.T/F/N, Tr.T/F/Mark, and every string in files marked
         "// @translate-all-strings") into Tools/lang/en.txt: one phrase per line, "id<TAB>english<TAB>where".
         The id is a hash of the English, so translations follow a phrase wherever it moves in the code.
check    Reports, for every Tools/lang/xx.txt, missing or stale phrases and broken {0} placeholders or tags.
build    extract + check, then writes Assets/Resources/Lang (keys.txt, one file per language, the fonts) and
         Assets/Scripts/Game/Locale/LanguageTable.cs.

The venv (once): python3 -m venv Tools/.external/pyenv && Tools/.external/pyenv/bin/pip install babel uharfbuzz fonttools
"""
import hashlib
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPTS = os.path.join(ROOT, "Assets", "Scripts")
LANG_DIR = os.path.join(ROOT, "Tools", "lang")

CALLS = {("Loc", "T"), ("Loc", "F"), ("Loc", "N"), ("Loc", "Plain"), ("Loc", "PlainF"), ("Tr", "T"), ("Tr", "F"), ("Tr", "Mark")}
ALL_STRINGS_MARK = "// @translate-all-strings"


# ---------------------------------------------------------------------------------------------------- C# scanning

def tokens(src):
    """Yields (kind, value, line) for a C# source: kind is 'str' (a regular or verbatim literal, unescaped),
    'istr' (an interpolated literal, raw), 'id', or 'p' (punctuation). Comments and char literals are skipped."""
    i, n, line = 0, len(src), 1
    while i < n:
        c = src[i]
        if c == "\n":
            line += 1
            i += 1
        elif c.isspace():
            i += 1
        elif src.startswith("//", i):
            j = src.find("\n", i)
            i = n if j < 0 else j
        elif src.startswith("/*", i):
            j = src.find("*/", i + 2)
            j = n if j < 0 else j + 2
            line += src.count("\n", i, j)
            i = j
        elif c in "$@" and i + 1 < n and src[i + 1] in '"$@':
            # $"..", @"..", $@"..", @$".."
            j = i
            prefix = ""
            while src[j] in "$@":
                prefix += src[j]
                j += 1
            start_line = line
            value, i, line = read_string(src, j, line, verbatim="@" in prefix)
            yield ("istr" if "$" in prefix else "str", value, start_line)
        elif c == '"':
            start_line = line
            value, i, line = read_string(src, i, line, verbatim=False)
            yield ("str", value, start_line)
        elif c == "'":
            j = i + 1
            while j < n and src[j] != "'":
                j += 2 if src[j] == "\\" else 1
            i = j + 1
        elif c.isalpha() or c == "_":
            j = i
            while j < n and (src[j].isalnum() or src[j] == "_"):
                j += 1
            yield ("id", src[i:j], line)
            i = j
        else:
            yield ("p", c, line)
            i += 1


ESCAPES = {"n": "\n", "t": "\t", "r": "\r", "0": "\0", '"': '"', "'": "'", "\\": "\\", "a": "\a", "b": "\b", "f": "\f", "v": "\v"}


def read_string(src, i, line, verbatim):
    """Reads the literal starting at the quote src[i]; returns (value, index after it, line)."""
    assert src[i] == '"'
    i += 1
    out = []
    while True:
        c = src[i]
        if c == "\n":
            line += 1
        if verbatim:
            if c == '"':
                if i + 1 < len(src) and src[i + 1] == '"':
                    out.append('"')
                    i += 2
                    continue
                return "".join(out), i + 1, line
            out.append(c)
            i += 1
            continue
        if c == "\\":
            e = src[i + 1]
            if e == "u":
                out.append(chr(int(src[i + 2:i + 6], 16)))
                i += 6
            elif e == "x":
                j = i + 2
                while j < len(src) and j < i + 6 and src[j] in "0123456789abcdefABCDEF":
                    j += 1
                out.append(chr(int(src[i + 2:j], 16)))
                i = j
            else:
                out.append(ESCAPES.get(e, e))
                i += 2
            continue
        if c == '"':
            return "".join(out), i + 1, line
        out.append(c)
        i += 1


def phrase_id(english):
    return hashlib.sha1(english.encode("utf-8")).hexdigest()[:7]


def escape(s):
    return s.replace("\\", "\\\\").replace("\n", "\\n").replace("\t", "\\t")


def unescape(s):
    out, i = [], 0
    while i < len(s):
        if s[i] == "\\" and i + 1 < len(s):
            out.append({"n": "\n", "t": "\t"}.get(s[i + 1], s[i + 1]))
            i += 2
        else:
            out.append(s[i])
            i += 1
    return "".join(out)


def extract():
    """Returns [(english, 'file:line')] in code order, each phrase once, and a list of problems."""
    found, seen, problems = [], set(), []
    files = []
    for base, dirs, names in os.walk(SCRIPTS):
        dirs.sort()
        if os.sep + "Editor" in base:
            continue
        for name in sorted(names):
            if name.endswith(".cs"):
                files.append(os.path.join(base, name))
    for path in files:
        src = open(path, encoding="utf-8").read()
        rel = os.path.relpath(path, ROOT)
        toks = list(tokens(src))
        take_all = ALL_STRINGS_MARK in src

        def add(text, line):
            if text and text not in seen:
                seen.add(text)
                found.append((text, f"{os.path.basename(path)}:{line}"))

        if take_all:
            for kind, value, line in toks:
                if kind == "str" and re.search(r"[A-Za-z]", value):
                    add(value, line)
                elif kind == "istr":
                    problems.append(f"{rel}:{line}: interpolated string in a translated file: {value[:60]}")
        for k in range(len(toks) - 4):
            a, dot, b, paren = toks[k], toks[k + 1], toks[k + 2], toks[k + 3]
            if a[0] != "id" or dot != ("p", ".", dot[2]) or b[0] != "id" or paren[:2] != ("p", "("):
                continue
            if (a[1], b[1]) not in CALLS:
                continue
            # the first argument: literal (+ literal)*
            j = k + 4
            parts = []
            while j < len(toks):
                if toks[j][0] == "str":
                    parts.append(toks[j][1])
                    j += 1
                    if j < len(toks) and toks[j][:2] == ("p", "+") and j + 1 < len(toks) and toks[j + 1][0] == "str":
                        j += 1
                        continue
                    break
                if toks[j][0] == "istr":
                    problems.append(f"{rel}:{toks[j][2]}: interpolated string passed to {a[1]}.{b[1]}: {toks[j][1][:60]}")
                break
            if parts:
                add("".join(parts), toks[k + 4][2])
    return found, problems


def read_lang(path):
    """{id: text} from a Tools/lang/xx.txt file."""
    entries = {}
    for raw in open(path, encoding="utf-8"):
        raw = raw.rstrip("\n")
        if not raw or raw.startswith("#"):
            continue
        parts = raw.split("\t")
        if len(parts) < 2:
            continue
        entries[parts[0]] = unescape(parts[1])
    return entries


def write_master(found):
    os.makedirs(LANG_DIR, exist_ok=True)
    path = os.path.join(LANG_DIR, "en.txt")
    with open(path, "w", encoding="utf-8") as f:
        f.write("# The English the game shows, collected by Tools/build_locale.py extract - do not edit, edit the code.\n")
        f.write("# id <TAB> English <TAB> where it is in the code. Translations: Tools/lang/<code>.txt, \"id<TAB>text\".\n")
        for text, where in found:
            f.write(f"{phrase_id(text)}\t{escape(text)}\t{where}\n")
    return path


def main(argv):
    command = argv[1] if len(argv) > 1 else "extract"
    found, problems = extract()
    for p in problems:
        print("PROBLEM", p)
    path = write_master(found)
    print(f"{len(found)} phrases -> {os.path.relpath(path, ROOT)}")
    if command in ("check", "build"):
        import locale_build
        locale_build.run(command, found)


if __name__ == "__main__":
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    main(sys.argv)
