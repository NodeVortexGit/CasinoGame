using System;
using System.Collections;
using System.Collections.Generic;
using Casino.Art;
using UnityEngine;
using UnityEngine.Rendering;

namespace Casino.World
{
    /// <summary>
    /// How real, and how much like a film, the casino looks at each graphics quality. Low is the baked look on its own;
    /// each step up adds to it:
    /// <list type="bullet">
    /// <item>Medium: polished marble, varnished wood, black lacquer and gold reflect the room; the waitress casts a
    /// soft shadow as she walks.</item>
    /// <item>High: the film look. Twice the texture detail, pools of light on the floor and soft beams of light coming
    /// down through a faint haze, bloom round the brightest lights, filmic tone mapping, a film grade (cool shadows,
    /// warm highlights), grain and a vignette, and staff lit by the lamp above them, with light glowing warm under the
    /// skin and the casino glinting in their eyes.</item>
    /// <item>Ultra: the cinema camera. Depth of field that focuses on the person or game you're about to use (else on
    /// whatever you look at) and pulls focus smoothly (the room melts away behind a face or the cards in front of you), the long horizontal streaks of an anamorphic
    /// lens on neon and bulbs, faint colour fringes at the edges of the frame, dust glinting in the beams, a slight
    /// handheld drift to the camera, four times the texture detail, sharper reflections, a finer, wider bloom, and
    /// everyone in the room animated however far away they are.</item>
    /// </list>
    /// </summary>
    public sealed partial class CasinoWorld
    {
        public const int QualityLow = 0, QualityMedium = 1, QualityHigh = 2, QualityUltra = 3;

        /// <summary>Where the room is photographed from for reflections: the middle of the floor, at eye height.</summary>
        static readonly Vector3 ReflectionPoint = new Vector3(0f, 1.7f, 1f);
        static readonly Color HazeColor = new Color(0.22f, 0.13f, 0.17f);
        const float HazeDensity = 0.012f;

        static readonly int ReflCubeId = Shader.PropertyToID("_ReflCube"), ReflBoxMinId = Shader.PropertyToID("_ReflBoxMin"),
            ReflBoxMaxId = Shader.PropertyToID("_ReflBoxMax"), ReflPosId = Shader.PropertyToID("_ReflPos"),
            ReflBlurId = Shader.PropertyToID("_ReflBlur"), HazeColorId = Shader.PropertyToID("_HazeColor"),
            HazeDensityId = Shader.PropertyToID("_HazeDensity"), GlowDimId = Shader.PropertyToID("_GlowDim");

        /// <summary>The surface textures, in the order of the surface kits (null = plain white).</summary>
        static readonly Func<int, Texture2D>[] SurfacePainters =
            { WorldArt.Carpet, WorldArt.Marble, WorldArt.Damask, WorldArt.Wood, null, WorldArt.Bottles, WorldArt.SlotReels };

        int quality = -1;        // the quality the world is set up for
        int textureDetail;       // the resolution the surface textures were painted at (0 = not yet)
        Material[] surfaceMaterials = new Material[0];
        RenderTexture reflections;
        WorldPost post;
        Texture2D shadowFalloff;
        Renderer lightPools, beams, motes;
        bool restyling;
        readonly List<Vector3> poolVerts = new List<Vector3>();
        readonly List<Vector2> poolUvs = new List<Vector2>();
        readonly List<Color> poolColors = new List<Color>();
        readonly List<Vector3> beamVerts = new List<Vector3>(), beamNormals = new List<Vector3>();
        readonly List<Vector2> beamUvs = new List<Vector2>();
        readonly List<Color> beamColors = new List<Color>();
        readonly List<int> beamTris = new List<int>();
        readonly GlowKit dust = new GlowKit();
        readonly System.Random dustRandom = new System.Random(7);

        static bool SupportsHdr => SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf);

        static int TextureDetailFor(int q) => q >= QualityUltra ? 4 : q == QualityHigh ? 2 : 1;
        static int ReflectionSizeFor(int q) => q >= QualityUltra ? 512 : 256;
        static int AnisoFor(int q) => q >= QualityUltra ? 8 : q == QualityHigh ? 4 : q == QualityMedium ? 2 : 1;

        /// <summary>Staff further away than this hold their pose instead of animating.</summary>
        float StaffAnimDistance => quality >= QualityUltra ? 60f : quality == QualityHigh ? 16f : quality == QualityMedium ? 11f : 8f;

        Texture2D ShadowFalloff => shadowFalloff != null ? shadowFalloff : shadowFalloff = WorldArt.Glow();

        /// <summary>Settings changed, or the world is starting: set the casino up for the chosen quality.</summary>
        public void ApplyGraphics()
        {
            if (Camera == null) return;
            Camera.allowMSAA = QualitySettings.antiAliasing > 0;
            quality = App.I.GraphicsQuality;

            SetKeyword("CASINO_REFLECT", quality >= QualityMedium && reflections != null);
            SetKeyword("CASINO_REAL", quality >= QualityHigh);
            Shader.SetGlobalColor(HazeColorId, HazeColor);
            Shader.SetGlobalFloat(HazeDensityId, quality >= QualityHigh ? HazeDensity : 0f);

            bool filmic = quality >= QualityHigh && SupportsHdr, cinema = filmic && quality >= QualityUltra;
            Camera.allowHDR = filmic;
            Shader.SetGlobalFloat(GlowDimId, filmic ? 0.3f : 0f);
            Camera.depthTextureMode = cinema ? DepthTextureMode.Depth : DepthTextureMode.None;   // for the depth of field
            if (post == null) post = Camera.gameObject.AddComponent<WorldPost>();
            post.enabled = filmic;
            post.FineBloom = cinema;
            post.BloomLevels = cinema ? 6 : 5;
            post.Grade = cinema ? 1f : 0.75f;
            post.Grain = cinema ? 0.032f : 0.022f;
            post.Vignette = cinema ? 0.32f : 0.27f;
            post.Streaks = cinema ? 0.35f : 0f;
            post.Aberration = cinema ? 0.006f : 0f;
            post.DepthOfField = cinema;

            if (waitress != null) waitress.ShowContactShadow(quality >= QualityMedium, ShadowFalloff);
            if (lightPools != null) lightPools.enabled = quality >= QualityHigh;
            if (beams != null) beams.enabled = quality >= QualityHigh;
            if (motes != null) motes.enabled = quality >= QualityUltra;
            if (Ready && !restyling) StartCoroutine(Restyle());
        }

        /// <summary>A pool of light on the floor (a beam from the ceiling landing), shown from High up.</summary>
        void LightPool(Vector3 center, float radius, Color light)
        {
            Vector3 x = Vector3.right * radius, z = Vector3.forward * radius;
            center.y = 0.012f;
            poolVerts.Add(center - x - z); poolVerts.Add(center - x + z); poolVerts.Add(center + x + z); poolVerts.Add(center + x - z);
            poolUvs.Add(new Vector2(0, 0)); poolUvs.Add(new Vector2(0, 1)); poolUvs.Add(new Vector2(1, 1)); poolUvs.Add(new Vector2(1, 0));
            for (int i = 0; i < 4; i++) poolColors.Add(light);
        }

        void BuildLightPools()
        {
            if (poolVerts.Count == 0) return;
            var tris = new int[poolVerts.Count / 4 * 6];
            for (int q = 0, v = 0; v < poolVerts.Count; q += 6, v += 4)
            {
                tris[q] = v; tris[q + 1] = v + 1; tris[q + 2] = v + 2;
                tris[q + 3] = v; tris[q + 4] = v + 2; tris[q + 5] = v + 3;
            }
            var mesh = new Mesh { name = "Light pools" };
            mesh.SetVertices(poolVerts);
            mesh.SetUVs(0, poolUvs);
            mesh.SetColors(poolColors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            lightPools = AddRenderer("Light pools", mesh, new Material(Resources.Load<Shader>("Shaders/CasinoLightPool")) { name = "Light pools" });
            lightPools.enabled = quality >= QualityHigh;
        }

        /// <summary>
        /// A beam of light from a downlight to the floor, lit up by the haze (shown from High up), with dust drifting
        /// in it (Ultra). A cone, widest at the floor, faded at its outline by the Casino/Beam shader.
        /// </summary>
        void Beam(Vector3 top, float topRadius, float bottomRadius, Color light)
        {
            const int Sides = 14;
            var bottom = new Vector3(top.x, 0.02f, top.z);
            float height = top.y - bottom.y, slope = (bottomRadius - topRadius) / height;
            int start = beamVerts.Count;
            for (int s = 0; s <= Sides; s++)
            {
                float a = s * Mathf.PI * 2f / Sides;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                var normal = (dir + Vector3.up * slope).normalized;   // the side leans outwards as the beam widens
                beamVerts.Add(top + dir * topRadius); beamVerts.Add(bottom + dir * bottomRadius);
                beamNormals.Add(normal); beamNormals.Add(normal);
                beamUvs.Add(new Vector2(s / (float)Sides, 0f)); beamUvs.Add(new Vector2(s / (float)Sides, 1f));
                beamColors.Add(light); beamColors.Add(light);
                if (s == 0) continue;
                int v = start + s * 2;
                beamTris.Add(v - 2); beamTris.Add(v); beamTris.Add(v - 1);
                beamTris.Add(v - 1); beamTris.Add(v); beamTris.Add(v + 1);
            }
            // Specks of dust in the beam, catching the light now and then as they drift.
            for (int i = 0; i < 24; i++)
            {
                float along = 0.2f + 0.7f * (float)dustRandom.NextDouble();
                float r = Mathf.Lerp(topRadius, bottomRadius, along) * 0.85f * Mathf.Sqrt((float)dustRandom.NextDouble());
                float a = (float)dustRandom.NextDouble() * Mathf.PI * 2f;
                var at = Vector3.Lerp(top, bottom, along) + new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r);
                dust.Add(at, light * 3.5f, 0.02f + 0.015f * (float)dustRandom.NextDouble(), (float)dustRandom.NextDouble() * 50f,
                    0.15f + 0.2f * (float)dustRandom.NextDouble());
            }
        }

        void BuildBeams()
        {
            if (beamVerts.Count == 0) return;
            var mesh = new Mesh { name = "Light beams" };
            mesh.SetVertices(beamVerts);
            mesh.SetNormals(beamNormals);
            mesh.SetUVs(0, beamUvs);
            mesh.SetColors(beamColors);
            mesh.SetTriangles(beamTris, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            beams = AddRenderer("Light beams", mesh, new Material(Resources.Load<Shader>("Shaders/CasinoBeam")) { name = "Light beams" });
            beams.enabled = quality >= QualityHigh;

            var dustMat = new Material(Resources.Load<Shader>("Shaders/CasinoDust")) { mainTexture = ShadowFalloff, name = "Dust" };
            motes = AddRenderer("Dust", dust.Build("Dust"), dustMat);
            motes.enabled = quality >= QualityUltra;
        }

        /// <summary>
        /// Ultra: like a camera operator, keep a face in focus (the room softens behind them): the member of staff you're
        /// about to talk to or play with, even behind their table or counter, else the face nearest the middle of the
        /// view; a game without staff (a slot machine) is focused on itself. With nobody in view, the post pass
        /// autofocuses on whatever is in the middle. While seated, nothing in front of the focus blurs, so the cards and
        /// chips on the table stay sharp while the dealer is in focus.
        /// </summary>
        void AimFocus()
        {
            if (post == null || !post.DepthOfField) return;
            post.NearBlur = seated ? 0f : 0.15f;
            Vector3 target = default;
            bool found = false;
            if (!seated && Focused != null && Focused.Staff != null) found = InView(target = Focused.Staff.Face);
            if (!found) found = FaceNearMiddle(out target);
            if (!found && !seated && Focused != null && Focused.Staff == null) found = InView(target = Focused.Focus);
            if (!found)
            {
                post.FocusTarget = Vector4.zero;
                return;
            }
            Vector3 v = Camera.WorldToViewportPoint(target);
            post.FocusTarget = new Vector4(v.x, v.y, v.z, 1f);
        }

        bool InView(Vector3 point)
        {
            Vector3 v = Camera.WorldToViewportPoint(point);
            return v.z > 0.4f && v.z < 14f && v.x > 0.03f && v.x < 0.97f && v.y > 0.03f && v.y < 0.97f;
        }

        /// <summary>The face of the member of staff nearest the middle of the view (a little above it, where faces sit).</summary>
        bool FaceNearMiddle(out Vector3 face)
        {
            face = default;
            float best = float.MaxValue;
            foreach (var fig in staff)
            {
                if (fig == null || !fig.gameObject.activeInHierarchy) continue;
                Vector3 at = fig.Face, v = Camera.WorldToViewportPoint(at);
                if (v.z < 0.4f || v.z > 12f) continue;
                float dx = v.x - 0.5f, dy = v.y - 0.58f;
                if (Mathf.Abs(dx) > 0.3f || Mathf.Abs(dy) > 0.38f) continue;
                float score = dx * dx * 4f + dy * dy * 2f + v.z * 0.01f;
                if (score >= best) continue;
                best = score;
                face = at;
            }
            return best < float.MaxValue;
        }

        float handheld;   // how much of the handheld drift is on (eases in and out)

        /// <summary>
        /// Ultra: the camera drifts very slightly, as if held by a camera operator rather than bolted to a rail
        /// (a fraction of a degree, slowly; never while seated at a game).
        /// </summary>
        Quaternion Handheld(float dt)
        {
            handheld = Mathf.MoveTowards(handheld, quality >= QualityUltra && !seated && TourTarget == null ? 1f : 0f, dt);
            if (handheld <= 0f) return Quaternion.identity;
            float t = Time.time;
            var drift = new Vector3(Mathf.PerlinNoise(t * 0.21f, 3.7f) - 0.5f, Mathf.PerlinNoise(9.1f, t * 0.17f) - 0.5f,
                Mathf.PerlinNoise(t * 0.13f, 6.2f) - 0.5f);
            return Quaternion.Euler(Vector3.Scale(drift, new Vector3(0.6f, 0.8f, 0.4f)) * handheld);
        }

        /// <summary>Repaints the textures and re-photographs the room when the quality needs a different resolution.</summary>
        IEnumerator Restyle()
        {
            restyling = true;
            while (true)
            {
                int detail = TextureDetailFor(quality);
                if (detail != textureDetail)
                {
                    yield return PaintSurfaces(detail);
                    continue;   // the quality may have changed again meanwhile
                }
                if (quality >= QualityMedium && (reflections == null || reflections.width != ReflectionSizeFor(quality)))
                    RenderReflections(ReflectionSizeFor(quality));
                break;
            }
            int aniso = AnisoFor(quality);
            foreach (var m in surfaceMaterials)
                if (m != null && m.mainTexture != null && m.mainTexture != Texture2D.whiteTexture) m.mainTexture.anisoLevel = aniso;
            SetKeyword("CASINO_REFLECT", quality >= QualityMedium && reflections != null);
            restyling = false;
        }

        /// <summary>The surface textures at this detail, one per frame (4x detail takes a moment on a phone).</summary>
        IEnumerator PaintSurfaces(int detail)
        {
            for (int i = 0; i < surfaceMaterials.Length && i < SurfacePainters.Length; i++)
            {
                var m = surfaceMaterials[i];
                if (m == null || SurfacePainters[i] == null) continue;
                var old = m.mainTexture;
                m.mainTexture = SurfacePainters[i](detail);
                if (old != null) Destroy(old);
                yield return null;
            }
            textureDetail = detail;
        }

        /// <summary>
        /// Photographs the room into a cubemap for the polished surfaces to reflect. Staff are left out (they move), and
        /// so are reflections and haze: the cubemap can't show itself, and the haze is added where it's looked at.
        /// </summary>
        void RenderReflections(int size)
        {
            if (reflections != null)
            {
                reflections.Release();
                Destroy(reflections);
            }
            bool hdr = SupportsHdr;
            reflections = new RenderTexture(size, size, 16, hdr ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32)
            {
                name = "Room reflections",
                dimension = TextureDimension.Cube,
                useMipMap = true,
                autoGenerateMips = false,
                filterMode = FilterMode.Trilinear,
            };
            reflections.Create();

            var go = new GameObject("Reflection camera");
            go.transform.SetParent(transform, false);
            go.transform.position = ReflectionPoint;
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Camera.backgroundColor;
            cam.cullingMask = Camera.cullingMask & ~(1 << StaffFigure.Layer);
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = Camera.farClipPlane;
            cam.allowHDR = hdr;
            cam.allowMSAA = false;

            SetKeyword("CASINO_REFLECT", false);
            Shader.SetGlobalFloat(HazeDensityId, 0f);
            cam.RenderToCubemap(reflections);
            Shader.SetGlobalFloat(HazeDensityId, quality >= QualityHigh ? HazeDensity : 0f);
            reflections.GenerateMips();
            Destroy(go);

            Shader.SetGlobalTexture(ReflCubeId, reflections);
            Shader.SetGlobalVector(ReflBoxMinId, new Vector4(MinX, -0.05f, MinZ, 0f));
            Shader.SetGlobalVector(ReflBoxMaxId, new Vector4(MaxX, CeilingY + 0.5f, MaxZ, 0f));
            Shader.SetGlobalVector(ReflPosId, ReflectionPoint);
            Shader.SetGlobalFloat(ReflBlurId, Mathf.Log(size, 2f) - 1f);
        }

        static void SetKeyword(string keyword, bool on)
        {
            if (on) Shader.EnableKeyword(keyword);
            else Shader.DisableKeyword(keyword);
        }

        /// <summary>How polished each surface is (shown at Medium and above).</summary>
        void SetFinishes()
        {
            marble.Gloss = 0.9f;      // polished stone
            wood.Gloss = 0.6f;        // varnished mahogany
            bottles.Gloss = 0.35f;    // glass shelves
            slotScreens.Gloss = 0.55f;
            plain.Gloss = 0.2f;
            plain.Finish(Gold, 0.75f, true);
            plain.Finish(Palette.Gold, 0.75f, true);
            plain.Finish(Lacquer, 0.85f);
            plain.Finish(Palette.Hex("2A1220"), 0.6f);   // lacquered doors
            plain.Finish(Leather, 0.35f);
            plain.Finish(Ceiling, 0f);
            plain.Finish(Palette.Hex("4A2260"), 0f);      // dome sides
            plain.Finish(Palette.Hex("A3122E"), 0.05f);   // velvet: runner, ropes, stools
            plain.Finish(Palette.Hex("0D5E3A"), 0f);      // roulette felt
            foreach (var t in Palette.Tables) plain.Finish(t.Felt, 0f);
        }

        // Smoothly follows the strongest lamp as the waitress walks past them.
        Vector3 waitressLightDir = Vector3.up;
        Color waitressLight = Color.black;
        float waitressLightCheck;
        Vector3 waitressLightTarget = Vector3.up;
        Color waitressLightTargetColor = Color.black;

        void LightWaitress(float dt)
        {
            if (waitress == null || quality < QualityHigh) return;
            if (Time.time >= waitressLightCheck)
            {
                waitressLightCheck = Time.time + 0.25f;
                (waitressLightTarget, waitressLightTargetColor) = rig.Strongest(waitress.transform.position + Vector3.up * 1.5f);
            }
            float k = 1f - Mathf.Exp(-dt * 3f);
            waitressLightDir = Vector3.Slerp(waitressLightDir, waitressLightTarget, k);
            waitressLight = Color.Lerp(waitressLight, waitressLightTargetColor, k);
            waitress.SetRoomLight(waitressLightDir, waitressLight);
        }
    }
}
