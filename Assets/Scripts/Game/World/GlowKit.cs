using System.Collections.Generic;
using UnityEngine;

namespace Casino.World
{
    /// <summary>
    /// Every light halo in the room (bulbs, chandelier crystals, neon, lamps) in one static mesh: each glow is a quad
    /// whose four corners sit at its centre and are spread out to face the camera by the Casino/Glow shader.
    /// </summary>
    public sealed class GlowKit
    {
        static readonly Vector2[] Corners = { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };

        readonly List<Vector3> pos = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<Vector4> data = new List<Vector4>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> tris = new List<int>();
        float maxSize;

        public int Count => pos.Count / 4;

        /// <param name="size">Diameter in metres.</param>
        /// <param name="depth">0 = steady, 1 = pulses fully on and off at <paramref name="speed"/> (radians/s).</param>
        public void Add(Vector3 at, Color color, float size, float phase = 0f, float speed = 0f, float depth = 0f)
        {
            int i = pos.Count;
            foreach (var c in Corners)
            {
                pos.Add(at);
                uv.Add(c);
                data.Add(new Vector4(size, phase, speed, depth));
                colors.Add(color);
            }
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
            maxSize = Mathf.Max(maxSize, size);
        }

        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name };
            if (pos.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(pos);
            mesh.SetUVs(0, uv);
            mesh.SetUVs(1, data);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            var b = mesh.bounds;
            b.Expand(maxSize);
            mesh.bounds = b;
            mesh.UploadMeshData(true);
            return mesh;
        }
    }
}
