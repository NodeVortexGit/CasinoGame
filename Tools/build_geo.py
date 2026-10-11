#!/usr/bin/env python3
"""Converts Natural Earth 1:50m admin-0 countries (public domain) into the compact
binary file the game uses for its offline GPS -> country lookup.

Usage: python3 Tools/build_geo.py ne_50m_admin_0_countries.geojson Assets/Resources/Geo/countries.bytes

Format (little endian):
  "CGEO" u16 version=1 u16 countryCount
  per country: 2 bytes ISO-3166 alpha-2, i16 minLon,minLat,maxLon,maxLat (x100),
               u16 ringCount, per ring: u16 pointCount, then pointCount x (i16 lon, i16 lat) (x100)
Point-in-country uses the even-odd rule over all rings, so holes need no flag.
"""
import json, struct, sys

FALLBACK = {"SOL": "SO", "CYN": "CY", "KAS": "IN"}  # Somaliland, N. Cyprus, Siachen

def iso2(props):
    for key in ("ISO_A2_EH", "ISO_A2", "WB_A2"):
        v = props.get(key)
        if v and v != "-99" and len(v) == 2:
            return v
    return FALLBACK.get(props.get("ADM0_A3"))

def perp_dist(p, a, b):
    (x, y), (x1, y1), (x2, y2) = p, a, b
    dx, dy = x2 - x1, y2 - y1
    if dx == 0 and dy == 0:
        return ((x - x1) ** 2 + (y - y1) ** 2) ** 0.5
    t = max(0, min(1, ((x - x1) * dx + (y - y1) * dy) / (dx * dx + dy * dy)))
    return ((x - x1 - t * dx) ** 2 + (y - y1 - t * dy) ** 2) ** 0.5

def simplify(pts, tol):
    if len(pts) < 4:
        return pts
    keep = [False] * len(pts)
    keep[0] = keep[-1] = True
    stack = [(0, len(pts) - 1)]
    while stack:
        s, e = stack.pop()
        best, idx = 0, -1
        for i in range(s + 1, e):
            d = perp_dist(pts[i], pts[s], pts[e])
            if d > best:
                best, idx = d, i
        if best > tol and idx > 0:
            keep[idx] = True
            stack += [(s, idx), (idx, e)]
    return [p for p, k in zip(pts, keep) if k]

def q(v):
    return max(-32767, min(32767, int(round(v * 100))))

def main(src, dst):
    feats = json.load(open(src))["features"]
    countries = {}
    for f in feats:
        code = iso2(f["properties"])
        if not code:
            continue
        g = f["geometry"]
        polys = g["coordinates"] if g["type"] == "MultiPolygon" else [g["coordinates"]]
        for poly in polys:
            for ring in poly:
                pts = simplify([tuple(p) for p in ring], 0.01)
                qp = []
                for lon, lat in pts:
                    p = (q(lon), q(lat))
                    if not qp or qp[-1] != p:
                        qp.append(p)
                if len(qp) >= 3:
                    countries.setdefault(code, []).append(qp)
    out = bytearray(b"CGEO") + struct.pack("<HH", 1, len(countries))
    total = 0
    for code in sorted(countries):
        rings = countries[code]
        xs = [p[0] for r in rings for p in r]; ys = [p[1] for r in rings for p in r]
        out += code.encode("ascii") + struct.pack("<hhhhH", min(xs), min(ys), max(xs), max(ys), len(rings))
        for r in rings:
            out += struct.pack("<H", len(r))
            for x, y in r:
                out += struct.pack("<hh", x, y)
            total += len(r)
    open(dst, "wb").write(out)
    print(f"{len(countries)} countries, {total} points, {len(out)} bytes -> {dst}")

if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
