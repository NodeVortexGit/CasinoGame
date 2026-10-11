using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Casino.World
{
    /// <summary>
    /// Collects the geometry for one material, bakes the room lighting into its vertex colours and builds the mesh.
    /// Quads take their corners as seen from the front: bottom-left, top-left, top-right, bottom-right.
    /// </summary>
    public sealed class MeshKit
    {
        readonly List<Vector3> pos = new List<Vector3>(), nrm = new List<Vector3>(), anim = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<Color> albedo = new List<Color>();
        readonly List<float> emission = new List<float>();
        readonly List<int> tris = new List<int>();

        /// <summary>World units per texture repeat (for the u/v of generated surfaces).</summary>
        public float Tile = 1f;

        /// <summary>Brightness animation for vertices added from now on (see the Casino/World shader).</summary>
        public Vector3 Anim;

        /// <summary>When above 0, vertices added from now on glow by themselves (albedo × emission) instead of being lit.</summary>
        public float Emission;

        public int VertexCount => pos.Count;

        int Vert(Vector3 p, Vector3 n, Vector2 t, Color c)
        {
            pos.Add(p);
            nrm.Add(n);
            uv.Add(t);
            albedo.Add(c);
            anim.Add(Anim);
            emission.Add(Emission);
            return pos.Count - 1;
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color) =>
            Quad(a, b, c, d, color, new Vector2(0, 0), new Vector2((d - a).magnitude / Tile, (b - a).magnitude / Tile));

        /// <summary>
        /// Upright textured panel (screen, shelf, poster) facing <paramref name="facing"/>, mapped so the texture reads
        /// correctly to someone looking at it.
        /// </summary>
        public void Panel(Vector3 center, Vector3 facing, float halfWidth, float halfHeight, Color color, Vector2 uvMax)
        {
            Vector3 right = Vector3.Cross(Vector3.up, -facing).normalized * halfWidth, up = Vector3.up * halfHeight;
            Quad(center - right - up, center - right + up, center + right + up, center + right - up, color, Vector2.zero, uvMax);
        }

        /// <summary>Quad whose winding is fixed up so its front faces <paramref name="outward"/>.</summary>
        public void QuadFacing(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, Color color)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, d - a), outward) >= 0f) Quad(a, b, c, d, color);
            else Quad(a, d, c, b, color);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color, Vector2 uv0, Vector2 uv1)
        {
            Vector3 n = Vector3.Cross(b - a, d - a).normalized;
            int i = Vert(a, n, uv0, color);
            Vert(b, n, new Vector2(uv0.x, uv1.y), color);
            Vert(c, n, uv1, color);
            Vert(d, n, new Vector2(uv1.x, uv0.y), color);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        /// <summary>A quad split into cells of about <paramref name="cell"/> metres, so baked light can form pools.</summary>
        public void Grid(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color, float cell = 1f)
        {
            float w = (d - a).magnitude, h = (b - a).magnitude;
            int nu = Mathf.Max(1, Mathf.CeilToInt(w / cell)), nv = Mathf.Max(1, Mathf.CeilToInt(h / cell));
            Vector3 n = Vector3.Cross(b - a, d - a).normalized;
            int start = pos.Count;
            for (int v = 0; v <= nv; v++)
            {
                float tv = v / (float)nv;
                Vector3 left = Vector3.Lerp(a, b, tv), right = Vector3.Lerp(d, c, tv);
                for (int u = 0; u <= nu; u++)
                {
                    float tu = u / (float)nu;
                    Vert(Vector3.Lerp(left, right, tu), n, new Vector2(tu * w / Tile, tv * h / Tile), color);
                }
            }
            int row = nu + 1;
            for (int v = 0; v < nv; v++)
                for (int u = 0; u < nu; u++)
                {
                    int i = start + v * row + u;
                    tris.Add(i); tris.Add(i + row); tris.Add(i + row + 1);
                    tris.Add(i); tris.Add(i + row + 1); tris.Add(i + 1);
                }
        }

        /// <summary>Axis-aligned box standing on <paramref name="bottomCenter"/>; <paramref name="cell"/> &gt; 0 subdivides the faces.</summary>
        public void Box(Vector3 bottomCenter, Vector3 size, Color color, bool top = true, bool bottom = false, float cell = 0f)
        {
            Vector3 h = size * 0.5f;
            Vector3 c = bottomCenter + new Vector3(0, h.y, 0);
            Vector3 p000 = c + new Vector3(-h.x, -h.y, -h.z), p100 = c + new Vector3(h.x, -h.y, -h.z);
            Vector3 p010 = c + new Vector3(-h.x, h.y, -h.z), p110 = c + new Vector3(h.x, h.y, -h.z);
            Vector3 p001 = c + new Vector3(-h.x, -h.y, h.z), p101 = c + new Vector3(h.x, -h.y, h.z);
            Vector3 p011 = c + new Vector3(-h.x, h.y, h.z), p111 = c + new Vector3(h.x, h.y, h.z);
            void Face(Vector3 a, Vector3 b, Vector3 cc, Vector3 d)
            {
                if (cell > 0f) Grid(a, b, cc, d, color, cell);
                else Quad(a, b, cc, d, color);
            }
            Face(p000, p010, p110, p100); // -z
            Face(p101, p111, p011, p001); // +z
            Face(p001, p011, p010, p000); // -x
            Face(p100, p110, p111, p101); // +x
            if (top) Face(p010, p011, p111, p110);
            if (bottom) Face(p001, p000, p100, p101);
        }

        /// <summary>Vertical cylinder (or cone when the radii differ) standing on <paramref name="bottomCenter"/>.</summary>
        public void Cylinder(Vector3 bottomCenter, float radius, float height, int segments, Color color, bool top = true,
            float topRadius = -1f, float fromDeg = 0f, float toDeg = 360f)
        {
            if (topRadius < 0f) topRadius = radius;
            int start = pos.Count;
            float arc = (toDeg - fromDeg) * Mathf.Deg2Rad;
            float circumference = arc * Mathf.Max(radius, topRadius);
            for (int i = 0; i <= segments; i++)
            {
                float a = fromDeg * Mathf.Deg2Rad + arc * i / segments;
                var dir = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                var n = new Vector3(dir.x, (radius - topRadius) / Mathf.Max(0.01f, height), dir.z).normalized;
                float u = circumference * i / segments / Tile;
                Vert(bottomCenter + dir * radius, n, new Vector2(u, 0), color);
                Vert(bottomCenter + dir * topRadius + Vector3.up * height, n, new Vector2(u, height / Tile), color);
            }
            for (int i = 0; i < segments; i++)
            {
                int b = start + i * 2;
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
                tris.Add(b); tris.Add(b + 3); tris.Add(b + 1);
            }
            if (top) Disc(bottomCenter + Vector3.up * height, topRadius, segments, color, fromDeg, toDeg);
        }

        /// <summary>Horizontal disc (or sector) facing up, or down when <paramref name="up"/> is false.</summary>
        public void Disc(Vector3 center, float radius, int segments, Color color, float fromDeg = 0f, float toDeg = 360f, bool up = true)
        {
            Vector3 n = up ? Vector3.up : Vector3.down;
            int c = Vert(center, n, new Vector2(center.x, center.z) / Tile, color);
            float arc = (toDeg - fromDeg) * Mathf.Deg2Rad;
            for (int i = 0; i <= segments; i++)
            {
                float a = fromDeg * Mathf.Deg2Rad + arc * i / segments;
                Vector3 p = center + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * radius;
                Vert(p, n, new Vector2(p.x, p.z) / Tile, color);
            }
            for (int i = 0; i < segments; i++)
            {
                if (up) { tris.Add(c); tris.Add(c + 1 + i); tris.Add(c + 2 + i); }
                else { tris.Add(c); tris.Add(c + 2 + i); tris.Add(c + 1 + i); }
            }
        }

        /// <summary>Flat ring (annulus) facing up.</summary>
        public void Ring(Vector3 center, float inner, float outer, int segments, Color color)
        {
            int start = pos.Count;
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.PI * 2f * i / segments;
                var dir = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                Vert(center + dir * inner, Vector3.up, new Vector2(i / (float)segments, 0), color);
                Vert(center + dir * outer, Vector3.up, new Vector2(i / (float)segments, 1), color);
            }
            for (int i = 0; i < segments; i++)
            {
                int b = start + i * 2;
                tris.Add(b); tris.Add(b + 3); tris.Add(b + 2);
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 3);
            }
        }

        /// <summary>A thin square tube between two points (neon strips, rails, trims).</summary>
        public void Bar(Vector3 from, Vector3 to, float thickness, Color color)
        {
            Vector3 dir = (to - from).normalized;
            Vector3 side = Vector3.Cross(dir, Mathf.Abs(dir.y) > 0.9f ? Vector3.right : Vector3.up).normalized * thickness * 0.5f;
            Vector3 up = Vector3.Cross(side, dir).normalized * thickness * 0.5f;
            Vector3 a0 = from - side - up, a1 = from - side + up, a2 = from + side + up, a3 = from + side - up;
            Vector3 b0 = to - side - up, b1 = to - side + up, b2 = to + side + up, b3 = to + side - up;
            Vector3 mid = (from + to) * 0.5f;
            QuadFacing(a0, b0, b1, a1, (a0 + b1) * 0.5f - mid, color);
            QuadFacing(a1, b1, b2, a2, (a1 + b2) * 0.5f - mid, color);
            QuadFacing(a2, b2, b3, a3, (a2 + b3) * 0.5f - mid, color);
            QuadFacing(a3, b3, b0, a0, (a3 + b0) * 0.5f - mid, color);
        }

        /// <summary>Bakes the lighting and returns the finished mesh.</summary>
        public Mesh Build(LightRig rig, string name)
        {
            var colors = new List<Color>(pos.Count);
            for (int i = 0; i < pos.Count; i++)
            {
                Color lit = emission[i] > 0f ? albedo[i] * emission[i] : albedo[i] * rig.Evaluate(pos[i], nrm[i]);
                // The shader doubles vertex colours: store half, so spots up to 2x brightness survive.
                colors.Add(new Color(Mathf.Clamp01(lit.r * 0.5f), Mathf.Clamp01(lit.g * 0.5f), Mathf.Clamp01(lit.b * 0.5f), 1f));
            }
            var mesh = new Mesh { name = name };
            if (pos.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(pos);
            mesh.SetUVs(0, uv);
            mesh.SetUVs(1, anim);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }
    }

    /// <summary>
    /// The casino's light, evaluated once per vertex at load time: a warm hemisphere ambient, point lights with soft
    /// wrap-around, and soft contact shadows on the floor under furniture.
    /// </summary>
    public sealed class LightRig
    {
        struct PointLight
        {
            public Vector3 Pos;
            public Color Color;
            public float Range;
        }

        struct Occluder
        {
            public Vector2 At;
            public Vector2 HalfSize;
            public float Strength;
        }

        readonly List<PointLight> lights = new List<PointLight>();
        readonly List<Occluder> occluders = new List<Occluder>();

        public Color AmbientUp = new Color(0.42f, 0.32f, 0.36f);
        public Color AmbientDown = new Color(0.2f, 0.12f, 0.16f);

        public void Light(Vector3 position, Color color, float range, float intensity = 1f) =>
            lights.Add(new PointLight { Pos = position, Color = color * intensity, Range = range });

        /// <summary>A soft shadow on the floor under something (table, counter, machine) of the given footprint.</summary>
        public void Shadow(Vector3 center, Vector2 halfSize, float strength = 0.45f) =>
            occluders.Add(new Occluder { At = new Vector2(center.x, center.z), HalfSize = halfSize, Strength = strength });

        public Color Evaluate(Vector3 p, Vector3 n)
        {
            Color c = Color.Lerp(AmbientDown, AmbientUp, n.y * 0.5f + 0.5f);
            for (int i = 0; i < lights.Count; i++)
            {
                var l = lights[i];
                Vector3 d = l.Pos - p;
                float dist = d.magnitude;
                if (dist >= l.Range) continue;
                float wrap = Mathf.Max(0f, Vector3.Dot(n, d / Mathf.Max(0.001f, dist))) * 0.75f + 0.25f;
                float att = 1f - dist / l.Range;
                c += l.Color * (wrap * att * att);
            }
            if (n.y > 0.7f && p.y < 0.05f)
            {
                for (int i = 0; i < occluders.Count; i++)
                {
                    var o = occluders[i];
                    float dx = Mathf.Max(0f, Mathf.Abs(p.x - o.At.x) - o.HalfSize.x);
                    float dz = Mathf.Max(0f, Mathf.Abs(p.z - o.At.y) - o.HalfSize.y);
                    float d = Mathf.Sqrt(dx * dx + dz * dz);
                    const float soft = 0.6f;
                    if (d < soft) c *= 1f - o.Strength * (1f - d / soft);
                }
            }
            return c;
        }
    }
}
