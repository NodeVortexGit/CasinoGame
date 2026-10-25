#!/usr/bin/env python3
"""Helps translate the phrases a language is still missing.

    Tools/.external/pyenv/bin/python Tools/translations.py todo <code> [out.txt]
    Tools/.external/pyenv/bin/python Tools/translations.py merge <code> <file.txt>
    Tools/.external/pyenv/bin/python Tools/translations.py prune

todo   Lists the phrases Tools/lang/<code>.txt is missing, "id<TAB>english<TAB>where", to stdout or out.txt.
merge  Adds the "id<TAB>text" lines of file.txt to Tools/lang/<code>.txt, replacing ids already there.
prune  Drops stale ids (ones no longer in en.txt) from every language file.

Run Tools/build_locale.py check first so en.txt matches the code, and again after merging.
"""
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LANG_DIR = os.path.join(ROOT, "Tools", "lang")


def master():
    """[(id, english, where)] from en.txt, in its order."""
    rows = []
    for raw in open(os.path.join(LANG_DIR, "en.txt"), encoding="utf-8"):
        raw = raw.rstrip("\n")
        if not raw or raw.startswith("#"):
            continue
        parts = raw.split("\t")
        rows.append((parts[0], parts[1], parts[2] if len(parts) > 2 else ""))
    return rows


def lang_path(code):
    path = os.path.join(LANG_DIR, f"{code}.txt")
    if not os.path.exists(path):
        sys.exit(f"no such language file: {os.path.relpath(path, ROOT)}")
    return path


def lines(path):
    return [raw.rstrip("\n") for raw in open(path, encoding="utf-8")]


def ids(path):
    return {l.split("\t", 1)[0] for l in lines(path) if l and not l.startswith("#")}


def todo(code, out=None):
    have = ids(lang_path(code))
    missing = [r for r in master() if r[0] not in have]
    text = "".join(f"{i}\t{en}\t{where}\n" for i, en, where in missing)
    if out:
        open(out, "w", encoding="utf-8").write(text)
        print(f"{code}: {len(missing)} missing -> {out}")
    else:
        sys.stdout.write(text)


def merge(code, src):
    known = {r[0] for r in master()}
    new = {}
    for n, l in enumerate(lines(src), 1):
        if not l.strip() or l.startswith("#"):
            continue
        i, sep, t = l.partition("\t")
        if not sep:
            i, _, t = l.partition(" ")
        if i not in known:
            sys.exit(f"{src}:{n}: unknown id {i!r}")
        if not t.strip():
            sys.exit(f"{src}:{n}: empty translation for {i}")
        new[i] = t.strip()
    path = lang_path(code)
    kept = [l for l in lines(path) if not l or l.startswith("#") or l.split("\t", 1)[0] not in new]
    kept += [f"{i}\t{t}" for i, t in new.items()]
    open(path, "w", encoding="utf-8").write("\n".join(kept).rstrip("\n") + "\n")
    print(f"{code}: {len(new)} merged")


def prune():
    known = {r[0] for r in master()}
    for name in sorted(os.listdir(LANG_DIR)):
        if not name.endswith(".txt") or name == "en.txt":
            continue
        path = os.path.join(LANG_DIR, name)
        old = lines(path)
        kept = [l for l in old if not l or l.startswith("#") or l.split("\t", 1)[0] in known]
        if len(kept) != len(old):
            open(path, "w", encoding="utf-8").write("\n".join(kept).rstrip("\n") + "\n")
            print(f"{name[:-4]}: {len(old) - len(kept)} stale removed")


if __name__ == "__main__":
    args = sys.argv[1:]
    if args[:1] == ["todo"] and len(args) in (2, 3):
        todo(*args[1:])
    elif args[:1] == ["merge"] and len(args) == 3:
        merge(args[1], args[2])
    elif args == ["prune"]:
        prune()
    else:
        sys.exit(__doc__)
