using System;
using UnityEngine;

namespace Casino.Art
{
    /// <summary>Signed distance functions (negative inside). Formulas after Inigo Quilez.</summary>
    public delegate float SdfShape(Vector2 p);

    public static class Sdf
    {
        public static SdfShape Circle(Vector2 c, float r) => p => (p - c).magnitude - r;

        public static SdfShape Ellipse(Vector2 c, Vector2 r) => p =>
        {
            Vector2 q = new Vector2((p.x - c.x) / r.x, (p.y - c.y) / r.y);
            return (q.magnitude - 1f) * Mathf.Min(r.x, r.y);
        };

        public static SdfShape Box(Vector2 c, Vector2 half, float radius = 0f) => p =>
        {
            Vector2 d = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half + new Vector2(radius, radius);
            return new Vector2(Mathf.Max(d.x, 0), Mathf.Max(d.y, 0)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0) - radius;
        };

        public static SdfShape Segment(Vector2 a, Vector2 b, float thickness) => p =>
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude - thickness;
        };

        /// <summary>Capsule from <paramref name="a"/> (radius ra) to <paramref name="b"/> (radius rb): limbs, tapered strokes.</summary>
        public static SdfShape Taper(Vector2 a, Vector2 b, float ra, float rb)
        {
            Vector2 ab = b - a;
            float h = Mathf.Max(1e-5f, ab.magnitude);
            Vector2 dir = ab / h;
            float k = (ra - rb) / h, c = Mathf.Sqrt(Mathf.Max(0f, 1f - k * k));
            return p =>
            {
                Vector2 d = p - a;
                var q = new Vector2(Mathf.Abs(d.x * dir.y - d.y * dir.x), d.x * dir.x + d.y * dir.y);
                float m = -k * q.x + c * q.y;
                if (m < 0f) return q.magnitude - ra;
                if (m > c * h) return (q - new Vector2(0f, h)).magnitude - rb;
                return c * q.x + k * q.y - ra;
            };
        }

        /// <summary>A chain of tapered capsules through the points (radius per point), smoothly joined.</summary>
        public static SdfShape Limb(Vector2[] points, float[] radii, float smooth = 0.004f)
        {
            SdfShape s = Taper(points[0], points[1], radii[0], radii[1]);
            for (int i = 1; i < points.Length - 1; i++) s = SmoothUnion(s, Taper(points[i], points[i + 1], radii[i], radii[i + 1]), smooth);
            return s;
        }

        public static SdfShape Ring(Vector2 c, float r, float thickness) => p => Mathf.Abs((p - c).magnitude - r) - thickness;

        /// <summary>Arc of radius r centred on c, symmetric around <paramref name="directionDeg"/>, spanning ±halfAngleDeg.</summary>
        public static SdfShape Arc(Vector2 c, float r, float thickness, float directionDeg, float halfAngleDeg)
        {
            float rot = (90f - directionDeg) * Mathf.Deg2Rad;
            float cs = Mathf.Cos(rot), sn = Mathf.Sin(rot);
            Vector2 sc = new Vector2(Mathf.Sin(halfAngleDeg * Mathf.Deg2Rad), Mathf.Cos(halfAngleDeg * Mathf.Deg2Rad));
            return p =>
            {
                Vector2 d = p - c;
                Vector2 q = new Vector2(Mathf.Abs(d.x * cs - d.y * sn), d.x * sn + d.y * cs);
                float dist = sc.y * q.x > sc.x * q.y ? (q - sc * r).magnitude : Mathf.Abs(q.magnitude - r);
                return dist - thickness;
            };
        }

        public static SdfShape Triangle(Vector2 p0, Vector2 p1, Vector2 p2) => p =>
        {
            Vector2 e0 = p1 - p0, e1 = p2 - p1, e2 = p0 - p2;
            Vector2 v0 = p - p0, v1 = p - p1, v2 = p - p2;
            Vector2 pq0 = v0 - e0 * Mathf.Clamp01(Vector2.Dot(v0, e0) / Vector2.Dot(e0, e0));
            Vector2 pq1 = v1 - e1 * Mathf.Clamp01(Vector2.Dot(v1, e1) / Vector2.Dot(e1, e1));
            Vector2 pq2 = v2 - e2 * Mathf.Clamp01(Vector2.Dot(v2, e2) / Vector2.Dot(e2, e2));
            float s = Mathf.Sign(e0.x * e2.y - e0.y * e2.x);
            Vector2 d = Vector2.Min(Vector2.Min(
                    new Vector2(Vector2.Dot(pq0, pq0), s * (v0.x * e0.y - v0.y * e0.x)),
                    new Vector2(Vector2.Dot(pq1, pq1), s * (v1.x * e1.y - v1.y * e1.x))),
                new Vector2(Vector2.Dot(pq2, pq2), s * (v2.x * e2.y - v2.y * e2.x)));
            return -Mathf.Sqrt(d.x) * Mathf.Sign(d.y);
        };

        public static SdfShape Polygon(params Vector2[] v) => p =>
        {
            float d = Vector2.Dot(p - v[0], p - v[0]);
            float s = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                Vector2 e = v[j] - v[i], w = p - v[i];
                Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, Vector2.Dot(b, b));
                bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        };

        /// <summary>Heart with its tip at <paramref name="tip"/>, about 1.2*size wide and 1.1*size tall.</summary>
        public static SdfShape Heart(Vector2 tip, float size, bool flipped = false) => p =>
        {
            Vector2 q = (p - tip) / size;
            if (flipped) q.y = -q.y;
            q.x = Mathf.Abs(q.x);
            float d;
            if (q.y + q.x > 1f)
                d = (q - new Vector2(0.25f, 0.75f)).magnitude - Mathf.Sqrt(2f) / 4f;
            else
            {
                float a = Vector2.Dot(q - new Vector2(0, 1), q - new Vector2(0, 1));
                Vector2 m = q - 0.5f * Mathf.Max(q.x + q.y, 0f) * Vector2.one;
                d = Mathf.Sqrt(Mathf.Min(a, Vector2.Dot(m, m))) * Mathf.Sign(q.x - q.y);
            }
            return d * size;
        };

        public static SdfShape Rhombus(Vector2 c, Vector2 b) => p =>
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y));
            Vector2 t = b - 2f * q;
            float h = Mathf.Clamp((t.x * b.x - t.y * b.y) / Vector2.Dot(b, b), -1f, 1f);
            float d = (q - 0.5f * new Vector2(b.x * (1f - h), b.y * (1f + h))).magnitude;
            return d * Mathf.Sign(q.x * b.y + q.y * b.x - b.x * b.y);
        };

        public static SdfShape Star5(Vector2 c, float r, float innerRatio) => p =>
        {
            Vector2 k1 = new Vector2(0.809016994375f, -0.587785252292f);
            Vector2 k2 = new Vector2(-k1.x, k1.y);
            Vector2 q = p - c;
            q.x = Mathf.Abs(q.x);
            q -= 2f * Mathf.Max(Vector2.Dot(k1, q), 0f) * k1;
            q -= 2f * Mathf.Max(Vector2.Dot(k2, q), 0f) * k2;
            q.x = Mathf.Abs(q.x);
            q.y -= r;
            Vector2 ba = innerRatio * new Vector2(-k1.y, k1.x) - new Vector2(0, 1);
            float h = Mathf.Clamp(Vector2.Dot(q, ba) / Vector2.Dot(ba, ba), 0f, r);
            return (q - ba * h).magnitude * Mathf.Sign(q.y * ba.x - q.x * ba.y);
        };

        public static SdfShape Union(SdfShape a, SdfShape b) => p => Mathf.Min(a(p), b(p));
        public static SdfShape Union(params SdfShape[] s) => p =>
        {
            float d = float.MaxValue;
            for (int i = 0; i < s.Length; i++) d = Mathf.Min(d, s[i](p));
            return d;
        };
        public static SdfShape Intersect(SdfShape a, SdfShape b) => p => Mathf.Max(a(p), b(p));
        public static SdfShape Subtract(SdfShape a, SdfShape b) => p => Mathf.Max(a(p), -b(p));

        public static SdfShape SmoothUnion(SdfShape a, SdfShape b, float k) => p =>
        {
            float da = a(p), db = b(p);
            float h = Mathf.Max(k - Mathf.Abs(da - db), 0f) / k;
            return Mathf.Min(da, db) - h * h * k * 0.25f;
        };

        public static SdfShape Rotate(SdfShape s, Vector2 pivot, float degrees)
        {
            float r = -degrees * Mathf.Deg2Rad, cs = Mathf.Cos(r), sn = Mathf.Sin(r);
            return p =>
            {
                Vector2 d = p - pivot;
                return s(new Vector2(d.x * cs - d.y * sn, d.x * sn + d.y * cs) + pivot);
            };
        }

        public static SdfShape Offset(SdfShape s, Vector2 by) => p => s(p - by);
        public static SdfShape Grow(SdfShape s, float amount) => p => s(p) - amount;
        public static SdfShape Outline(SdfShape s, float thickness) => p => Mathf.Abs(s(p)) - thickness;
        public static SdfShape HalfPlaneAbove(float y) => p => y - p.y;
        public static SdfShape HalfPlaneBelow(float y) => p => p.y - y;
    }

    /// <summary>
    /// Minimal CPU rasteriser: paints SDF shapes with analytic anti-aliasing into a texture.
    /// Coordinates are normalised: (0,0) bottom-left, (1,1) top-right.
    /// </summary>
    public sealed class SdfCanvas
    {
        readonly Color[] px;
        readonly float pixel;

        Vector2 viewOrigin;
        float viewScale = 1f;
        bool hasView;

        /// <summary>
        /// Draws the following shapes in a local space: canvas point = origin + local * scale. Used to put the portrait
        /// head onto a full-body figure. <see cref="ResetView"/> goes back to canvas space.
        /// </summary>
        public void SetView(Vector2 origin, float scale)
        {
            viewOrigin = origin;
            viewScale = scale;
            hasView = true;
        }

        public void ResetView()
        {
            viewOrigin = Vector2.zero;
            viewScale = 1f;
            hasView = false;
        }

        public SdfCanvas(int width, int height)
        {
            Width = width;
            Height = height;
            px = new Color[width * height];
            pixel = 1f / Mathf.Max(width, height);
        }

        public int Width { get; }
        public int Height { get; }

        /// <summary>Normalised point for pixel centre (aspect-correct: x spans 0..Width/Max, y 0..Height/Max).</summary>
        Vector2 Point(int x, int y) => new Vector2((x + 0.5f) * pixel, (y + 0.5f) * pixel);

        public void Fill(SdfShape shape, Color color, float softness = 1f, Rect? bounds = null) =>
            Paint(shape, _ => color, softness, bounds);

        public void Paint(SdfShape shape, Func<Vector2, Color> colorAt, float softness = 1f, Rect? bounds = null)
        {
            if (hasView)
            {
                SdfShape local = shape;
                Func<Vector2, Color> localColor = colorAt;
                Vector2 o = viewOrigin;
                float k = viewScale, invScale = 1f / viewScale;
                shape = q => local((q - o) * invScale) * k;
                colorAt = q => localColor((q - o) * invScale);
                if (bounds.HasValue)
                {
                    Rect b = bounds.Value;
                    bounds = new Rect(o.x + b.x * k, o.y + b.y * k, b.width * k, b.height * k);
                }
            }
            int x0 = 0, y0 = 0, x1 = Width, y1 = Height;
            if (bounds.HasValue)
            {
                Rect b = bounds.Value;
                x0 = Mathf.Clamp(Mathf.FloorToInt(b.xMin / pixel) - 2, 0, Width);
                x1 = Mathf.Clamp(Mathf.CeilToInt(b.xMax / pixel) + 2, 0, Width);
                y0 = Mathf.Clamp(Mathf.FloorToInt(b.yMin / pixel) - 2, 0, Height);
                y1 = Mathf.Clamp(Mathf.CeilToInt(b.yMax / pixel) + 2, 0, Height);
            }
            softness = Mathf.Max(0.01f, softness);
            float inv = 1f / (pixel * softness);
            // Every operator used here returns a lower bound of the true distance, so a block whose centre is
            // further than its half-diagonal plus the anti-aliasing band cannot touch the shape and is skipped.
            const int block = 8;
            float skip = pixel * (block * 0.7072f + 0.5f * softness);
            for (int by = y0; by < y1; by += block)
            {
                for (int bx = x0; bx < x1; bx += block)
                {
                    int ex = Mathf.Min(bx + block, x1), ey = Mathf.Min(by + block, y1);
                    Vector2 centre = new Vector2((bx + ex) * 0.5f * pixel, (by + ey) * 0.5f * pixel);
                    if (shape(centre) > skip) continue;
                    for (int y = by; y < ey; y++)
                    {
                        for (int x = bx; x < ex; x++)
                        {
                            Vector2 p = Point(x, y);
                            float coverage = Mathf.Clamp01(0.5f - shape(p) * inv);
                            if (coverage <= 0f) continue;
                            Blend(y * Width + x, colorAt(p), coverage);
                        }
                    }
                }
            }
        }

        void Blend(int i, Color src, float coverage)
        {
            float sa = src.a * coverage;
            if (sa <= 0f) return;
            Color dst = px[i];
            float oa = sa + dst.a * (1f - sa);
            if (oa <= 0f) return;
            float k = dst.a * (1f - sa);
            px[i] = new Color((src.r * sa + dst.r * k) / oa, (src.g * sa + dst.g * k) / oa, (src.b * sa + dst.b * k) / oa, oa);
        }

        public void Clear(Color c)
        {
            for (int i = 0; i < px.Length; i++) px[i] = c;
        }

        /// <summary>Multiplies alpha by a mask shape (inside = keep), e.g. to clip a portrait into a circle.</summary>
        public void Mask(SdfShape shape, float softness = 1f)
        {
            float inv = 1f / (pixel * softness);
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    int i = y * Width + x;
                    px[i].a *= Mathf.Clamp01(0.5f - shape(Point(x, y)) * inv);
                }
        }

        public Color[] Pixels => px;

#if !ART_PREVIEW
        public Texture2D ToTexture(string name, FilterMode filter = FilterMode.Bilinear, bool mipmaps = false)
        {
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, mipmaps)
            {
                name = name,
                filterMode = filter,
                wrapMode = TextureWrapMode.Clamp,
            };
            tex.SetPixels(px);
            tex.Apply(mipmaps, true);
            return tex;
        }
#endif
    }
}
