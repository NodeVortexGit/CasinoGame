using System.Collections;
using System.Collections.Generic;
using Casino.Art;
using Casino.Core;
using Casino.Core.Economy;
using Casino.Core.Player;
using Casino.Screens;
using Casino.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Casino.World
{
    /// <summary>
    /// The casino floor (28 x 22 m, no windows): entrance and host in the south, cashier by the door, the bar along the
    /// west wall, four blackjack tables in the middle, slots along the east and north walls, roulette (coming soon) in
    /// the north. Everything is generated here; light is baked into vertex colours.
    /// </summary>
    public sealed partial class CasinoWorld
    {
        public const float MinX = -14f, MaxX = 14f, MinZ = -11f, MaxZ = 11f, CeilingY = 5.2f;

        static readonly Color Gold = new Color(1f, 0.76f, 0.3f);
        static readonly Color Lacquer = Palette.Hex("1A1220");
        static readonly Color Leather = Palette.Hex("4A1A14");
        static readonly Color Ceiling = Palette.Hex("3A1A4C");
        static readonly Color Pink = Palette.Hex("FF3FA4"), Cyan = Palette.Hex("39E6FF"), Violet = Palette.Hex("9B4DFF"), Warm = new Color(1f, 0.82f, 0.6f);

        MeshKit carpet, marble, damask, wood, plain, bottles, slotScreens;
        GlowKit glows;
        LightRig rig;
        readonly List<Obstacle> obstacles = new List<Obstacle>();
        readonly List<Spot> spots = new List<Spot>();
        readonly List<StaffFigure> staff = new List<StaffFigure>();
        readonly List<Vector2> patrol = new List<Vector2>();
        StaffFigure waitress;
        int hostIndex = -1;
        Transform rouletteWheel;
        RectTransform rouletteDecor;

        // Where each game is, so a game screen can seat the player there.
        readonly Dictionary<string, (Vector3 center, int dealer)> blackjackTables = new Dictionary<string, (Vector3, int)>();
        readonly List<(Vector3 screen, Vector3 facing)> slotMachines = new List<(Vector3, Vector3)>();
        static readonly Vector3 RouletteCenter = new Vector3(0f, 0f, 8.6f);
        int croupierIndex = -1;

        IEnumerator Build()
        {
            rig = new LightRig
            {
                AmbientUp = new Color(0.62f, 0.48f, 0.52f),
                AmbientDown = new Color(0.32f, 0.2f, 0.26f),
            };
            carpet = new MeshKit { Tile = 2.4f };
            marble = new MeshKit { Tile = 1.2f };
            damask = new MeshKit { Tile = 1.4f };
            wood = new MeshKit { Tile = 1.5f };
            plain = new MeshKit();
            bottles = new MeshKit();
            slotScreens = new MeshKit();
            glows = new GlowKit();

            Room();
            Ceilings();
            Entrance();
            yield return null;
            Cashier();
            Bar();
            yield return null;
            var tables = TableCatalog.Blackjack;
            for (int i = 0; i < tables.Count && i < 4; i++) BlackjackTable(tables[i], TablePosition(i));
            yield return null;
            Slots();
            Roulette();
            Pillars();
            yield return null;

            var textures = new[] { WorldArt.Carpet(), WorldArt.Marble(), WorldArt.Damask(), WorldArt.Wood(), Texture2D.whiteTexture, WorldArt.Bottles(), WorldArt.SlotReels() };
            var kits = new[] { carpet, marble, damask, wood, plain, bottles, slotScreens };
            var worldShader = Resources.Load<Shader>("Shaders/CasinoWorld");
            for (int i = 0; i < kits.Length; i++)
            {
                if (kits[i].VertexCount == 0) continue;
                var mat = new Material(worldShader) { mainTexture = textures[i], name = textures[i].name };
                AddRenderer("Static " + textures[i].name, kits[i].Build(rig, textures[i].name), mat);
                yield return null;
            }
            var glowMat = new Material(Resources.Load<Shader>("Shaders/CasinoGlow")) { mainTexture = WorldArt.Glow(), name = "Glow" };
            AddRenderer("Glows", glows.Build("Glows"), glowMat);
            yield return null;

            yield return SpawnAllStaff();
        }

        void AddRenderer(string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        static Vector3 TablePosition(int i) => new Vector3(i % 2 == 0 ? -4.5f : 4.5f, 0f, i < 2 ? -1f : 5f);

        // ------------------------------------------------------------------ shell

        void Room()
        {
            // Floor: marble for the entrance runway and the bar, carpet everywhere else (no overlaps).
            void Floor(MeshKit kit, float x0, float z0, float x1, float z1) =>
                kit.Grid(new Vector3(x0, 0, z0), new Vector3(x0, 0, z1), new Vector3(x1, 0, z1), new Vector3(x1, 0, z0), Color.white, 0.5f);
            Floor(marble, -2.5f, MinZ, 2.5f, -6f);
            Floor(marble, MinX, MinZ, -9f, MaxZ);
            Floor(carpet, -9f, MinZ, -2.5f, MaxZ);
            Floor(carpet, -2.5f, -6f, 2.5f, MaxZ);
            Floor(carpet, 2.5f, MinZ, MaxX, MaxZ);
            // red velvet runner up the entrance
            plain.Quad(new Vector3(-1.1f, 0.006f, MinZ), new Vector3(-1.1f, 0.006f, -6f), new Vector3(1.1f, 0.006f, -6f), new Vector3(1.1f, 0.006f, MinZ), Palette.Hex("A3122E"));
            plain.Bar(new Vector3(-1.15f, 0.012f, MinZ), new Vector3(-1.15f, 0.012f, -6f), 0.05f, Gold);
            plain.Bar(new Vector3(1.15f, 0.012f, MinZ), new Vector3(1.15f, 0.012f, -6f), 0.05f, Gold);

            // Walls, seen from inside: wood wainscot, damask, a dark cove with neon at the top. No windows anywhere.
            Wall(new Vector3(MinX, 0, MinZ), new Vector3(MaxX, 0, MinZ), 0);   // south
            Wall(new Vector3(MaxX, 0, MaxZ), new Vector3(MinX, 0, MaxZ), 1);   // north
            Wall(new Vector3(MinX, 0, MaxZ), new Vector3(MinX, 0, MinZ), 2);   // west
            Wall(new Vector3(MaxX, 0, MinZ), new Vector3(MaxX, 0, MaxZ), 3);   // east

            // Neon-lit, coloured light washing down from the coves
            for (float x = -12f; x <= 12f; x += 6f)
            {
                rig.Light(new Vector3(x, 4.3f, MinZ + 0.6f), Pink, 5f, 0.45f);
                rig.Light(new Vector3(x, 4.3f, MaxZ - 0.6f), Violet, 5f, 0.45f);
            }
            for (float z = -8f; z <= 8f; z += 6f)
            {
                rig.Light(new Vector3(MinX + 0.6f, 4.3f, z), Cyan, 5f, 0.35f);
                rig.Light(new Vector3(MaxX - 0.6f, 4.3f, z), Pink, 5f, 0.35f);
            }
        }

        /// <summary>A wall from a to b (left to right as seen from inside the room).</summary>
        void Wall(Vector3 a, Vector3 b, int side)
        {
            Vector3 up = Vector3.up;
            Vector3 along = (b - a).normalized;
            Vector3 inward = Vector3.Cross(along, up);
            if (Vector3.Dot(inward, -a - b) < 0) inward = -inward;   // the room is centred on the origin
            if (Vector3.Dot(Vector3.Cross(up, along), inward) < 0)
            {
                // Walls are built left to right as seen from inside, so their faces point into the room.
                (a, b) = (b, a);
                along = -along;
            }
            float len = (b - a).magnitude;

            wood.Grid(a, a + up * 1.1f, b + up * 1.1f, b, Color.white, 1f);
            damask.Grid(a + up * 1.1f, a + up * 4.45f, b + up * 4.45f, b + up * 1.1f, Color.white, 1f);
            plain.Grid(a + up * 4.45f, a + up * CeilingY, b + up * CeilingY, b + up * 4.45f, Ceiling, 2f);

            Vector3 o = inward * 0.03f;
            plain.Bar(a + up * 1.1f + o, b + up * 1.1f + o, 0.07f, Gold);
            plain.Bar(a + up * 0.08f + o, b + up * 0.08f + o, 0.12f, Lacquer);
            plain.Bar(a + up * 4.45f + o, b + up * 4.45f + o, 0.09f, Gold);
            // neon strip in the cove, slowly pulsing
            Color neon = side == 0 ? Pink : side == 1 ? Violet : side == 2 ? Cyan : Pink;
            plain.Emission = 1.6f;
            plain.Anim = new Vector3(side, 0.25f, 0.9f);
            plain.Bar(a + up * 4.75f + inward * 0.08f, b + up * 4.75f + inward * 0.08f, 0.06f, neon);
            plain.Emission = 0f;
            plain.Anim = Vector3.zero;
            for (float d = 0.8f; d < len; d += 1.6f)
                glows.Add(a + along * d + up * 4.75f + inward * 0.2f, neon * 0.3f, 2f, d * 0.5f + side, 0.9f, 0.25f);

            // wall sconces
            for (float d = 2f; d < len - 1f; d += 3.5f)
            {
                Vector3 p = a + along * d + up * 2.55f + inward * 0.12f;
                plain.Box(p - up * 0.2f - inward * 0.07f, new Vector3(0.1f, 0.3f, 0.04f), Gold);
                plain.Bar(p - up * 0.12f - inward * 0.06f, p - up * 0.02f, 0.025f, Gold);
                plain.Emission = 1.5f;
                plain.Cylinder(p, 0.11f, 0.16f, 10, new Color(1f, 0.78f, 0.55f), true, 0.065f);
                plain.Emission = 0f;
                glows.Add(p + up * 0.08f, Warm * 0.45f, 1.5f);
                rig.Light(p + inward * 0.3f, Warm, 2.4f, 0.35f);
            }
        }

        void Ceilings()
        {
            // Main ceiling with a raised, cove-lit centre over the tables.
            void Ceil(float x0, float z0, float x1, float z1, float y) =>
                plain.Grid(new Vector3(x0, y, z1), new Vector3(x0, y, z0), new Vector3(x1, y, z0), new Vector3(x1, y, z1), Ceiling, 1f);
            const float dx0 = -7.5f, dx1 = 7.5f, dz0 = -3.5f, dz1 = 8f, domeY = 6.2f;
            Ceil(MinX, MinZ, MaxX, dz0, CeilingY);
            Ceil(MinX, dz1, MaxX, MaxZ, CeilingY);
            Ceil(MinX, dz0, dx0, dz1, CeilingY);
            Ceil(dx1, dz0, MaxX, dz1, CeilingY);
            Ceil(dx0, dz0, dx1, dz1, domeY);
            // dome sides (facing inwards/down) and its colour-chasing LED cove
            var c0 = new Vector3(dx0, CeilingY, dz0); var c1 = new Vector3(dx1, CeilingY, dz0);
            var c2 = new Vector3(dx1, CeilingY, dz1); var c3 = new Vector3(dx0, CeilingY, dz1);
            Vector3 lift = Vector3.up * (domeY - CeilingY);
            plain.QuadFacing(c0, c0 + lift, c1 + lift, c1, Vector3.forward, Palette.Hex("4A2260"));
            plain.QuadFacing(c1, c1 + lift, c2 + lift, c2, Vector3.left, Palette.Hex("4A2260"));
            plain.QuadFacing(c2, c2 + lift, c3 + lift, c3, Vector3.back, Palette.Hex("4A2260"));
            plain.QuadFacing(c3, c3 + lift, c0 + lift, c0, Vector3.right, Palette.Hex("4A2260"));
            var corners = new[] { c0, c1, c2, c3, c0 };
            for (int e = 0; e < 4; e++)
            {
                Vector3 a = corners[e] + Vector3.up * 0.05f, b = corners[e + 1] + Vector3.up * 0.05f;
                plain.Emission = 1.8f;
                plain.Anim = new Vector3(e * 1.5f, 0.5f, 1.6f);
                plain.Bar(a, b, 0.07f, e % 2 == 0 ? Cyan : Pink);
                plain.Emission = 0f;
                plain.Anim = Vector3.zero;
                float len = (b - a).magnitude;
                for (float d = 0.5f; d < len; d += 1f)
                    glows.Add(Vector3.Lerp(a, b, d / len) - Vector3.up * 0.1f, (e % 2 == 0 ? Cyan : Pink) * 0.3f, 1.3f, d * 0.8f + e * 1.5f, 1.6f, 0.5f);
            }

            // Recessed downlights in a grid, pools of warm light on the floor.
            for (float x = -12f; x <= 12.1f; x += 2.5f)
                for (float z = -9.5f; z <= 9.6f; z += 2.5f)
                {
                    bool inDome = x > dx0 && x < dx1 && z > dz0 && z < dz1;
                    float y = inDome ? domeY : CeilingY;
                    plain.Emission = 2f;
                    plain.Disc(new Vector3(x, y - 0.01f, z), 0.13f, 10, Warm, 0, 360, false);
                    plain.Emission = 0f;
                    glows.Add(new Vector3(x, y - 0.06f, z), Warm * 0.45f, 1f);
                }
            for (float x = -10f; x <= 10f; x += 5f)
                for (float z = -8f; z <= 8f; z += 5f)
                    rig.Light(new Vector3(x, 4.6f, z), Warm, 5f, 0.3f);

            // Three big chandeliers over the tables.
            Chandelier(new Vector3(0f, 4.6f, 2f), 1.3f, domeY);
            Chandelier(new Vector3(-4.5f, 4.4f, 2f), 0.9f, domeY);
            Chandelier(new Vector3(4.5f, 4.4f, 2f), 0.9f, domeY);
        }

        void Chandelier(Vector3 c, float size, float ceilingY)
        {
            plain.Bar(c + Vector3.up * 0.3f, new Vector3(c.x, ceilingY, c.z), 0.04f, Gold);
            float[] radii = { 1f, 0.68f, 0.38f };
            for (int tier = 0; tier < radii.Length; tier++)
            {
                float r = radii[tier] * size, y = c.y - tier * 0.32f * size;
                var center = new Vector3(c.x, y, c.z);
                plain.Emission = 1.2f;
                plain.Ring(center, r - 0.03f, r + 0.03f, 24, Gold);
                plain.Emission = 0f;
                int drops = Mathf.RoundToInt(16 * radii[tier] * size);
                for (int i = 0; i < drops; i++)
                {
                    float a = (i + tier * 0.5f) / drops * Mathf.PI * 2f;
                    Vector3 p = center + new Vector3(Mathf.Sin(a) * r, -0.12f, Mathf.Cos(a) * r);
                    plain.Emission = 2.2f;
                    plain.Box(p - Vector3.up * 0.04f, new Vector3(0.025f, 0.07f, 0.025f), new Color(1f, 0.95f, 0.85f));
                    plain.Emission = 0f;
                    glows.Add(p, new Color(1f, 0.9f, 0.75f) * 0.5f, 0.5f, i * 1.7f + tier, 3.5f, 0.6f);
                }
            }
            glows.Add(c - Vector3.up * 0.35f * size, new Color(1f, 0.8f, 0.55f) * 0.42f, 4.2f * size, 0f, 1.2f, 0.12f);
            glows.Add(c - Vector3.up * 0.35f * size, new Color(1f, 0.95f, 0.85f) * 0.6f, 1.6f * size);
            rig.Light(c - Vector3.up * 0.5f, new Color(1f, 0.85f, 0.65f), 9f * size, 1.0f);
        }

        // ------------------------------------------------------------------ entrance and host

        void Entrance()
        {
            // Golden double doors in the south wall (they lead outside, so they stay shut and windowless).
            float z = MinZ + 0.02f;
            plain.Box(new Vector3(0, 0, z + 0.06f), new Vector3(3.4f, 3.4f, 0.12f), Gold);
            plain.Box(new Vector3(-0.8f, 0.05f, z + 0.14f), new Vector3(1.5f, 3.1f, 0.05f), Palette.Hex("2A1220"));
            plain.Box(new Vector3(0.8f, 0.05f, z + 0.14f), new Vector3(1.5f, 3.1f, 0.05f), Palette.Hex("2A1220"));
            plain.Bar(new Vector3(-0.2f, 1.2f, z + 0.2f), new Vector3(-0.2f, 1.9f, z + 0.2f), 0.06f, Gold);
            plain.Bar(new Vector3(0.2f, 1.2f, z + 0.2f), new Vector3(0.2f, 1.9f, z + 0.2f), 0.06f, Gold);

            // "Welcome" marquee above the doors, ringed with chasing bulbs.
            Marquee(new Vector3(0, 4.05f, z + 0.25f), 5.6f, 1.1f, Vector3.forward, Gold);
            Sign(GameConfig.CasinoName.ToUpperInvariant(), new Vector3(0, 4.12f, z + 0.3f), 180f, 5.2f, 0.75f, Palette.GoldLight, FontKind.Display);
            Sign("WELCOME", new Vector3(0, 3.66f, z + 0.3f), 180f, 4f, 0.3f, Pink, FontKind.Display);
            rig.Light(new Vector3(0, 3.6f, z + 1.4f), Gold, 5f, 0.7f);

            // Gold stanchions and velvet ropes lining the runway.
            for (int s = -1; s <= 1; s += 2)
                for (float zz = MinZ + 1f; zz <= -6.5f; zz += 1.5f)
                {
                    var post = new Vector3(s * 1.6f, 0, zz);
                    plain.Cylinder(post, 0.035f, 0.95f, 8, Gold);
                    plain.Emission = 0.6f;
                    plain.Box(post + Vector3.up * 0.95f, new Vector3(0.09f, 0.09f, 0.09f), Gold);
                    plain.Emission = 0f;
                    if (zz + 1.5f <= -6.4f)
                        plain.Bar(post + Vector3.up * 0.82f, post + new Vector3(0, 0.78f, 1.5f), 0.05f, Palette.Hex("A3122E"));
                }

            // Host podium, left of the runway.
            var podium = new Vector3(-2.6f, 0, -7.4f);
            wood.Box(podium, new Vector3(0.8f, 1.05f, 0.5f), Color.white);
            plain.Box(podium + Vector3.up * 1.05f, new Vector3(0.9f, 0.05f, 0.6f), Gold);
            plain.Emission = 1.2f;
            plain.Box(podium + new Vector3(0, 0.6f, -0.26f), new Vector3(0.6f, 0.06f, 0.02f), Pink);
            plain.Emission = 0f;
            obstacles.Add(Obstacle.Round(-2.9f, -7.2f, 0.75f));
            hostIndex = SpawnStaff(new Vector3(-3.35f, 0, -7.1f), 160f, StaffRole.Host, 0, StaffPose.Host);
            spots.Add(new Spot
            {
                Title = "TALK TO THE HOST",
                Subtitle = "Directions and tips",
                Icon = Icon.Person,
                Style = ButtonStyle.Blue,
                Focus = new Vector3(-3f, 1.3f, -7.2f),
                TapRadius = 1f,
                UseDistance = 3f,
                Approach = new Vector2(-2.6f, -5.6f),
                ApproachYaw = 200f,
                Use = HostTips,
                StaffIndex = hostIndex,
            });
            rig.Light(podium + new Vector3(0.4f, 2.6f, -0.8f), new Color(1f, 0.75f, 0.85f), 3.5f, 0.8f);
            rig.Shadow(podium, new Vector2(0.4f, 0.25f));
        }

        // ------------------------------------------------------------------ cashier

        void Cashier()
        {
            // A gilded booth just inside the entrance, on the right, with a service window facing the runway.
            const float x0 = 3.8f, x1 = 7.6f, z0 = -10.2f, z1 = -6.6f;
            float cz = (z0 + z1) * 0.5f;
            // back and side walls of the booth
            damask.Box(new Vector3(x1 - 0.1f, 0, cz), new Vector3(0.2f, 3.4f, z1 - z0), Color.white, true, false, 1f);
            damask.Box(new Vector3((x0 + x1) * 0.5f, 0, z1 - 0.1f), new Vector3(x1 - x0, 3.4f, 0.2f), Color.white, true, false, 1f);
            damask.Box(new Vector3((x0 + x1) * 0.5f, 0, z0 + 0.1f), new Vector3(x1 - x0, 3.4f, 0.2f), Color.white, true, false, 1f);
            // counter facing west
            wood.Box(new Vector3(x0 + 0.4f, 0, cz), new Vector3(0.8f, 1.1f, z1 - z0), Color.white);
            marble.Box(new Vector3(x0 + 0.4f, 1.1f, cz), new Vector3(0.95f, 0.06f, z1 - z0 + 0.1f), Color.white);
            plain.Emission = 1.4f;
            plain.Anim = new Vector3(0f, 0.35f, 2f);
            plain.Bar(new Vector3(x0 - 0.02f, 0.2f, z0 + 0.1f), new Vector3(x0 - 0.02f, 0.2f, z1 - 0.1f), 0.05f, Gold);
            plain.Anim = Vector3.zero;
            plain.Emission = 0f;
            // gold window bars and the header
            plain.Bar(new Vector3(x0 + 0.05f, 1.16f, z0 + 0.06f), new Vector3(x0 + 0.05f, 2.7f, z0 + 0.06f), 0.05f, Gold);
            plain.Bar(new Vector3(x0 + 0.05f, 1.16f, z1 - 0.06f), new Vector3(x0 + 0.05f, 2.7f, z1 - 0.06f), 0.05f, Gold);
            plain.Box(new Vector3(x0 + 0.2f, 2.7f, cz), new Vector3(0.4f, 0.7f, z1 - z0 + 0.2f), Lacquer);
            plain.Box(new Vector3((x0 + x1) * 0.5f, 3.4f, cz), new Vector3(x1 - x0 + 0.2f, 0.1f, z1 - z0 + 0.2f), Gold);
            Marquee(new Vector3(x0 - 0.02f, 3.05f, cz), z1 - z0, 0.7f, Vector3.left, Gold);
            Sign("CASHIER", new Vector3(x0 - 0.04f, 3.1f, cz), 90f, 3.2f, 0.55f, Palette.GoldLight, FontKind.Display);
            Sign("CHIPS  •  BONUSES", new Vector3(x0 - 0.04f, 2.78f, cz), 90f, 3.2f, 0.2f, Color.white, FontKind.Display);
            // a wall of chip stacks behind the cashier
            for (int i = 0; i < 18; i++)
            {
                float zz = z0 + 0.5f + (i % 9) * 0.32f, yy = 1.3f + (i / 9) * 0.5f;
                plain.Cylinder(new Vector3(x1 - 0.35f, yy, zz), 0.12f, 0.08f + (i * 37 % 5) * 0.06f, 10, Palette.ChipColors[i % Palette.ChipColors.Length]);
            }
            plain.Box(new Vector3(x1 - 0.35f, 1.2f, cz), new Vector3(0.4f, 0.08f, 3.2f), Gold);
            plain.Box(new Vector3(x1 - 0.35f, 1.7f, cz), new Vector3(0.4f, 0.08f, 3.2f), Gold);

            rig.Light(new Vector3(x0 + 1.4f, 2.8f, cz), new Color(1f, 0.82f, 0.45f), 4.5f, 1.2f);
            rig.Light(new Vector3(x0 - 1.2f, 2.4f, cz), new Color(1f, 0.85f, 0.6f), 3.5f, 0.7f);
            rig.Shadow(new Vector3(x0 + 0.4f, 0, cz), new Vector2(0.45f, (z1 - z0) * 0.5f));
            obstacles.Add(Obstacle.Rect(x0, z0, x1 + 0.3f, z1));

            int cashier = SpawnStaff(new Vector3(x0 + 1.0f, 0, cz), -90f, StaffRole.Cashier, 0, StaffPose.Counter);
            spots.Add(new Spot
            {
                Title = "BUY CHIPS",
                Subtitle = "Cashier",
                Icon = Icon.Coin,
                Style = ButtonStyle.Gold,
                Focus = new Vector3(x0 + 0.4f, 1.3f, cz),
                TapRadius = 1.6f,
                UseDistance = 3.2f,
                Approach = new Vector2(x0 - 1.0f, cz),
                ApproachYaw = 90f,
                Use = () => App.I.UI.Modal<ShopModal>(m => m.StartTab = ShopTab.Chips),
                StaffIndex = cashier,
            });
        }

        // ------------------------------------------------------------------ bar

        void Bar()
        {
            const float front = -9.7f, back = -10.6f, z0 = -5f, z1 = 5f;
            float cz = (z0 + z1) * 0.5f;
            // counter: wood body, marble top, glowing purple kick strip
            wood.Box(new Vector3((front + back) * 0.5f, 0, cz), new Vector3(front - back, 1.1f, z1 - z0), Color.white, true, false, 1f);
            marble.Box(new Vector3((front + back) * 0.5f, 1.1f, cz), new Vector3(front - back + 0.2f, 0.06f, z1 - z0 + 0.2f), Color.white);
            plain.Emission = 1.6f;
            plain.Anim = new Vector3(0f, 0.4f, 1.3f);
            plain.Bar(new Vector3(front + 0.02f, 0.12f, z0), new Vector3(front + 0.02f, 0.12f, z1), 0.06f, Violet);
            plain.Bar(new Vector3(front + 0.02f, 1.0f, z0), new Vector3(front + 0.02f, 1.0f, z1), 0.04f, Violet);
            plain.Anim = Vector3.zero;
            plain.Emission = 0f;
            for (float z = z0 + 0.5f; z < z1; z += 1f) glows.Add(new Vector3(front + 0.15f, 0.15f, z), Violet * 0.35f, 0.9f, z, 1.3f, 0.4f);

            // back bar: glowing bottle shelves and a neon sign
            const float shelfX = MinX + 0.35f;
            plain.Box(new Vector3(shelfX, 0, cz), new Vector3(0.6f, 1.0f, z1 - z0), Lacquer);
            bottles.Emission = 1.25f;
            bottles.Panel(new Vector3(shelfX + 0.31f, 2.2f, cz), Vector3.right, (z1 - z0) * 0.5f, 1.1f, Color.white, new Vector2(3f, 1f));
            bottles.Emission = 0f;
            plain.Box(new Vector3(shelfX, 3.3f, cz), new Vector3(0.8f, 0.12f, z1 - z0 + 0.2f), Gold);
            Sign("THE BAR", new Vector3(shelfX + 0.4f, 3.95f, cz), -90f, 4.5f, 0.85f, Pink, FontKind.Display);
            Sign("COCKTAILS  •  SNACKS  •  ENERGY", new Vector3(shelfX + 0.4f, 3.45f, cz), -90f, 5f, 0.22f, Cyan, FontKind.Display);
            Marquee(new Vector3(shelfX + 0.38f, 3.8f, cz), 5.4f, 1.15f, Vector3.right, Pink);

            // pendant lamps over the counter
            for (float z = -3f; z <= 3f; z += 3f)
            {
                var lamp = new Vector3((front + back) * 0.5f, 2.6f, z);
                plain.Bar(lamp, new Vector3(lamp.x, CeilingY, z), 0.02f, Gold);
                plain.Emission = 1.8f;
                plain.Cylinder(lamp - Vector3.up * 0.25f, 0.22f, 0.25f, 12, new Color(1f, 0.55f, 0.85f), true, 0.05f);
                plain.Emission = 0f;
                glows.Add(lamp - Vector3.up * 0.25f, new Color(1f, 0.5f, 0.85f) * 0.6f, 1.4f);
                rig.Light(lamp - Vector3.up * 0.4f, new Color(1f, 0.6f, 0.85f), 3.5f, 0.8f);
            }
            rig.Light(new Vector3(shelfX + 1.2f, 2.2f, cz - 3f), Violet, 5f, 0.8f);
            rig.Light(new Vector3(shelfX + 1.2f, 2.2f, cz + 3f), Cyan, 5f, 0.6f);

            // stools
            for (float z = z0 + 0.6f; z < z1; z += 1.3f)
            {
                var s = new Vector3(front + 0.65f, 0, z);
                plain.Cylinder(s, 0.04f, 0.72f, 8, Gold, false);
                plain.Ring(s + Vector3.up * 0.3f, 0.14f, 0.18f, 12, Gold);
                plain.Cylinder(s + Vector3.up * 0.72f, 0.22f, 0.08f, 14, Palette.Hex("A3122E"));
                rig.Shadow(s, new Vector2(0.12f, 0.12f), 0.3f);
            }
            rig.Shadow(new Vector3((front + back) * 0.5f, 0, cz), new Vector2(0.5f, (z1 - z0) * 0.5f));
            obstacles.Add(Obstacle.Rect(MinX, z0 - 0.2f, front + 0.85f, z1 + 0.2f));

            int bartender = SpawnStaff(new Vector3(back - 0.5f, 0, 0.6f), 90f, StaffRole.Server, 0, StaffPose.Polish);
            spots.Add(new Spot
            {
                Title = "FOOD & DRINKS",
                Subtitle = "The Bar",
                Icon = Icon.Food,
                Style = ButtonStyle.Purple,
                Focus = new Vector3(front, 1.2f, cz),
                TapRadius = 2.2f,
                UseDistance = 3.4f,
                Approach = new Vector2(front + 1.25f, 0.6f),
                ApproachYaw = -90f,
                Use = () => App.I.UI.Modal<ShopModal>(m => m.StartTab = ShopTab.Food),
                StaffIndex = bartender,
            });
        }

        // ------------------------------------------------------------------ blackjack tables

        void BlackjackTable(TableDef def, Vector3 c)
        {
            var theme = Palette.Tables[def.Theme % Palette.Tables.Length];
            const float r = 1.5f;
            // kidney-ish table: curved side to the players (south), straight side to the dealer (north)
            wood.Cylinder(c, r - 0.35f, 0.72f, 24, Color.white, false, r - 0.05f, 90f, 270f);
            plain.Cylinder(c + Vector3.up * 0.72f, r + 0.1f, 0.12f, 28, Leather, false, r + 0.12f, 90f, 270f);
            plain.Disc(c + Vector3.up * 0.84f, r + 0.12f, 28, Leather, 90f, 270f);
            plain.Disc(c + Vector3.up * 0.845f, r - 0.1f, 28, theme.Felt, 90f, 270f);
            wood.QuadFacing(c + new Vector3(-r - 0.12f, 0, 0), c + new Vector3(-r - 0.12f, 0.845f, 0), c + new Vector3(r + 0.12f, 0.845f, 0), c + new Vector3(r + 0.12f, 0, 0),
                Vector3.forward, Color.white);
            // betting circles, chip tray and shoe
            for (int i = 0; i < 5; i++)
            {
                float a = (126f + i * 27f) * Mathf.Deg2Rad;
                var spot = c + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * (r - 0.45f) + Vector3.up * 0.85f;
                plain.Emission = 0.8f;
                plain.Ring(spot, 0.13f, 0.15f, 16, theme.Accent);
                plain.Emission = 0f;
            }
            plain.Box(c + new Vector3(0, 0.845f, -0.2f), new Vector3(0.8f, 0.05f, 0.22f), Lacquer);
            for (int i = 0; i < 6; i++)
                plain.Cylinder(c + new Vector3(-0.33f + i * 0.13f, 0.89f, -0.2f), 0.05f, 0.05f + (i % 3) * 0.02f, 8, Palette.ChipColors[i % Palette.ChipColors.Length]);
            plain.Box(c + new Vector3(0.75f, 0.845f, -0.25f), new Vector3(0.2f, 0.12f, 0.3f), Palette.Hex("A3122E"));
            plain.Box(c + new Vector3(-0.75f, 0.845f, -0.25f), new Vector3(0.2f, 0.07f, 0.3f), Lacquer);   // discard holder
            SignFlat("BLACKJACK PAYS 3 TO 2", c + new Vector3(0, 0.852f, -0.62f), 1.8f, 0.14f, Palette.WithAlpha(Palette.GoldLight, 0.85f));
            SignFlat("DEALER MUST STAND ON 17", c + new Vector3(0, 0.852f, -0.82f), 1.6f, 0.09f, Palette.WithAlpha(Color.white, 0.7f));

            // stools on the players' side
            for (int i = 0; i < 5; i++)
            {
                float a = (126f + i * 27f) * Mathf.Deg2Rad;
                var s = c + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * (r + 0.55f);
                plain.Cylinder(s, 0.04f, 0.65f, 8, Gold, false);
                plain.Cylinder(s + Vector3.up * 0.65f, 0.21f, 0.09f, 14, theme.Felt * 0.8f + Leather * 0.4f);
                rig.Shadow(s, new Vector2(0.12f, 0.12f), 0.3f);
            }

            // hanging lamp and sign: name and stakes, ringed by bulbs
            var lamp = c + new Vector3(0, 2.9f, -0.2f);
            plain.Box(lamp, new Vector3(2.4f, 0.32f, 0.6f), Lacquer);
            plain.Emission = 1.6f;
            plain.Quad(lamp + new Vector3(-1.1f, -0.005f, 0.25f), lamp + new Vector3(-1.1f, -0.005f, -0.25f), lamp + new Vector3(1.1f, -0.005f, -0.25f), lamp + new Vector3(1.1f, -0.005f, 0.25f), Warm);
            plain.Emission = 0f;
            plain.Bar(lamp + new Vector3(-0.9f, 0.32f, 0), new Vector3(c.x - 0.9f, CeilingY + 1f, c.z - 0.2f), 0.02f, Gold);
            plain.Bar(lamp + new Vector3(0.9f, 0.32f, 0), new Vector3(c.x + 0.9f, CeilingY + 1f, c.z - 0.2f), 0.02f, Gold);
            Marquee(lamp + new Vector3(0, 0.16f, -0.31f), 2.4f, 0.32f, Vector3.back, theme.Accent, 0.09f);
            Sign($"{theme.Label}  {ChipFormat.Short(def.MinBet)}-{ChipFormat.Short(def.MaxBet)}", lamp + new Vector3(0, 0.17f, -0.32f), 0f, 2.3f, 0.26f, theme.Accent, FontKind.Display);
            glows.Add(lamp - Vector3.up * 0.1f, Warm * 0.5f, 2.6f);
            rig.Light(lamp - Vector3.up * 0.4f, new Color(1f, 0.92f, 0.8f), 4.2f, 1.25f);
            rig.Light(c + new Vector3(0, 1.6f, -2.2f), new Color(1f, 0.8f, 0.7f), 3f, 0.45f);
            rig.Shadow(c, new Vector2(r * 0.8f, r * 0.45f), 0.5f);

            obstacles.Add(Obstacle.Round(c.x, c.z - 0.25f, 2.0f));
            obstacles.Add(Obstacle.Rect(c.x - r - 0.2f, c.z - 0.2f, c.x + r + 0.2f, c.z + 1.1f));

            int dealer = SpawnStaff(c + new Vector3(0, 0, 0.42f), 180f, StaffRole.Dealer, def.Theme, StaffPose.Counter);
            blackjackTables[def.Id] = (c, dealer);
            spots.Add(new Spot
            {
                Title = "PLAY BLACKJACK",
                Subtitle = $"{theme.Label} table  •  bets {ChipFormat.Short(def.MinBet)} to {ChipFormat.Short(def.MaxBet)}",
                Icon = Icon.Cards,
                Style = ButtonStyle.Gold,
                Focus = c + Vector3.up * 1f,
                TapRadius = 1.7f,
                UseDistance = 3.3f,
                Approach = new Vector2(c.x, c.z - 2.65f),
                ApproachYaw = 0f,
                Use = () => SitAt(def),
                StaffIndex = dealer,
            });
        }

        // ------------------------------------------------------------------ slots and roulette

        void Slots()
        {
            Color[] cabinets = { Palette.Hex("B3122E"), Palette.Hex("5B2A9E"), Palette.Hex("124F86"), Palette.Hex("0D7A45"), Palette.Hex("C2471A") };
            int n = 0;
            void Machine(Vector3 p, Vector3 facing)
            {
                Color cab = cabinets[n % cabinets.Length];
                bool alongX = Mathf.Abs(facing.x) > 0.5f;
                var size = alongX ? new Vector3(0.8f, 1.65f, 0.95f) : new Vector3(0.95f, 1.65f, 0.8f);
                plain.Box(p, size, cab);
                plain.Box(p + Vector3.up * 1.65f, alongX ? new Vector3(0.7f, 0.35f, 0.9f) : new Vector3(0.9f, 0.35f, 0.7f), Lacquer);
                // screen
                Vector3 f = p + facing * 0.405f + Vector3.up * 1.0f;
                slotScreens.Emission = 1.3f;
                slotScreens.Anim = new Vector3(n * 1.3f, 0.3f, 3f + (n % 3));
                slotScreens.Panel(f, facing, 0.42f, 0.23f, Color.white, Vector2.one);
                slotMachines.Add((f, facing));
                slotScreens.Anim = Vector3.zero;
                slotScreens.Emission = 0f;
                // topper light and buttons
                plain.Emission = 2f;
                plain.Anim = new Vector3(n * 0.9f, 0.8f, 4f);
                Color top = n % 2 == 0 ? Pink : Cyan;
                plain.Box(p + Vector3.up * 2.0f + facing * 0.1f, alongX ? new Vector3(0.3f, 0.22f, 0.7f) : new Vector3(0.7f, 0.22f, 0.3f), top);
                plain.Anim = Vector3.zero;
                plain.Box(p + Vector3.up * 0.62f + facing * 0.42f, alongX ? new Vector3(0.1f, 0.05f, 0.5f) : new Vector3(0.5f, 0.05f, 0.1f), Palette.Gold);
                plain.Emission = 0f;
                glows.Add(p + Vector3.up * 2.1f + facing * 0.3f, top * 0.5f, 1.3f, n * 0.9f, 4f, 0.7f);
                glows.Add(f + facing * 0.1f, new Color(0.8f, 0.6f, 1f) * 0.25f, 1.2f, n * 1.3f, 3f, 0.3f);
                int machine = n;
                spots.Add(new Spot
                {
                    Title = "PLAY SLOTS",
                    Subtitle = "Lucky Ace 7s  •  10 lines",
                    Icon = Icon.Star,
                    Style = ButtonStyle.Purple,
                    Focus = p + Vector3.up * 1.2f + facing * 0.3f,
                    TapRadius = 0.55f,
                    UseDistance = 1.9f,
                    Approach = new Vector2(p.x + facing.x * 1.45f, p.z + facing.z * 1.45f),
                    ApproachYaw = Mathf.Atan2(-facing.x, -facing.z) * Mathf.Rad2Deg,
                    Use = () => OpenSlots(machine),
                });
                // stool
                var stool = p + facing * 0.95f;
                plain.Cylinder(stool, 0.04f, 0.65f, 8, Gold, false);
                plain.Cylinder(stool + Vector3.up * 0.65f, 0.2f, 0.08f, 12, Palette.Hex("A3122E"));
                rig.Shadow(p, new Vector2(0.5f, 0.5f));
                n++;
            }
            for (float z = -3f; z <= 9.2f; z += 1.1f) Machine(new Vector3(MaxX - 0.5f, 0, z), Vector3.left);
            for (float x = 3f; x <= 11.5f; x += 1.1f) Machine(new Vector3(x, 0, MaxZ - 0.5f), Vector3.back);
            for (float z = -2f; z <= 9f; z += 3.5f) rig.Light(new Vector3(MaxX - 1.5f, 2.6f, z), z % 2 == 0 ? Pink : Violet, 4f, 0.6f);
            for (float x = 4f; x <= 11f; x += 3.5f) rig.Light(new Vector3(x, 2.6f, MaxZ - 1.5f), Cyan, 4f, 0.5f);
            obstacles.Add(Obstacle.Rect(MaxX - 1.5f, -3.7f, MaxX, 9.8f));
            obstacles.Add(Obstacle.Rect(2.4f, MaxZ - 1.5f, MaxX, MaxZ));

            Marquee(new Vector3(MaxX - 0.05f, 3.55f, 3f), 6f, 1f, Vector3.left, Pink);
            Sign("SLOTS", new Vector3(MaxX - 0.08f, 3.65f, 3f), 90f, 5f, 0.7f, Pink, FontKind.Display);
            Sign("LUCKY ACE 7s  •  PAYS UP TO 2,500×", new Vector3(MaxX - 0.08f, 3.2f, 3f), 90f, 5f, 0.24f, Color.white, FontKind.Display);
        }

        void Roulette()
        {
            // The table follows the game's own layout (RouletteScreen): the same wheel and board you play on lie on it.
            var c = RouletteCenter;
            float k = RouletteScreen.LayerScale;
            Vector3 L(Vector2 p) => c + new Vector3(p.x * k, 0f, p.y * k);
            Vector3 mid = L(new Vector2(36f, -7f));
            var size = new Vector2(1872f * k, 726f * k);
            wood.Box(mid, new Vector3(size.x - 0.12f, 0.8f, size.y - 0.12f), Color.white);
            plain.Box(mid + Vector3.up * 0.8f, new Vector3(size.x, 0.06f, size.y), Leather);
            var boardSize = RouletteScreen.BoardSize;
            plain.Box(L(RouletteScreen.BoardPos) + Vector3.up * 0.86f, new Vector3((boardSize.x + 50f) * k, 0.008f, (boardSize.y + 50f) * k), Palette.Hex("0D5E3A"));
            // The wheel sits in a wooden bowl with a gold rim.
            Vector3 w = L(RouletteScreen.WheelPos);
            float wr = RouletteScreen.WheelSize * 0.5f * k;
            wood.Cylinder(w + Vector3.up * 0.8f, wr + 0.07f, RouletteScreen.TableHeight - 0.802f, 28, Color.white, false);
            plain.Emission = 0.4f;
            plain.Ring(w + Vector3.up * (RouletteScreen.TableHeight + 0.006f), wr - 0.004f, wr + 0.07f, 32, Gold);
            plain.Emission = 0f;
            rouletteDecor = WorldTableDecor(c);

            // stools for the players along the front
            for (int i = 0; i < 4; i++)
            {
                var s = c + new Vector3(-1.1f + i * 0.85f, 0, -size.y * 0.5f - 0.45f);
                plain.Cylinder(s, 0.04f, 0.65f, 8, Gold, false);
                plain.Cylinder(s + Vector3.up * 0.65f, 0.21f, 0.09f, 14, Palette.Hex("A3122E"));
                rig.Shadow(s, new Vector2(0.12f, 0.12f), 0.3f);
            }
            var signAt = c + new Vector3(0, 3.1f, 0.3f);
            plain.Box(signAt - Vector3.up * 0.25f, new Vector3(3.2f, 0.7f, 0.12f), Lacquer);
            plain.Bar(signAt + new Vector3(-1.2f, 0.45f, 0), new Vector3(c.x - 1.2f, CeilingY, c.z + 0.3f), 0.02f, Gold);
            plain.Bar(signAt + new Vector3(1.2f, 0.45f, 0), new Vector3(c.x + 1.2f, CeilingY, c.z + 0.3f), 0.02f, Gold);
            Marquee(signAt + new Vector3(0, 0.1f, -0.07f), 3.2f, 0.7f, Vector3.back, Gold, 0.1f);
            Sign("ROULETTE", signAt + new Vector3(0, 0.2f, -0.08f), 0f, 3f, 0.42f, Palette.GoldLight, FontKind.Display);
            Sign("PLACE YOUR BETS", signAt + new Vector3(0, -0.12f, -0.08f), 0f, 3f, 0.17f, Pink, FontKind.Display);
            rig.Light(c + new Vector3(0, 2.6f, -0.5f), Warm, 4.5f, 1f);
            rig.Light(w + new Vector3(0, 2.2f, 0.2f), Warm, 2.5f, 0.6f);
            rig.Shadow(mid, new Vector2(size.x * 0.5f, size.y * 0.5f));
            obstacles.Add(Obstacle.Rect(mid.x - size.x * 0.5f - 0.1f, mid.z - size.y * 0.5f - 0.1f, mid.x + size.x * 0.5f + 0.1f, mid.z + size.y * 0.5f + 0.6f));
            // The croupier stands behind the wheel.
            croupierIndex = SpawnStaff(new Vector3(w.x, 0, mid.z + size.y * 0.5f + 0.32f), 180f, StaffRole.Dealer,
                Screens.RouletteScreen.CroupierTheme, StaffPose.Counter);
            spots.Add(new Spot
            {
                Title = "PLAY ROULETTE",
                Subtitle = "European wheel  •  straight up pays 35 to 1",
                Icon = Icon.Play,
                Style = ButtonStyle.Gold,
                Focus = c + Vector3.up,
                TapRadius = 1.8f,
                UseDistance = 3.4f,
                Approach = new Vector2(c.x + 0.3f, c.z - 1.75f),
                ApproachYaw = 0f,
                Use = OpenRoulette,
                StaffIndex = croupierIndex,
            });
        }

        /// <summary>The wheel and board lying on the roulette table (for looking at; the game screen brings its own).</summary>
        RectTransform WorldTableDecor(Vector3 c)
        {
            var layer = WorldTable.CreateLayer("RouletteLayout", transform, c + Vector3.up * RouletteScreen.TableHeight,
                Quaternion.Euler(90f, 0f, 0f), RouletteScreen.LayerScale, new Vector2(1920, 1080), Camera, false);
            rouletteWheel = RouletteScreen.PaintWheel(layer, false, out var ball);
            ball.enabled = false;
            var board = RouletteScreen.PaintBoard(layer);
            foreach (var def in RouletteScreen.Layout()) RouletteScreen.PaintSpot(board, def, out _);
            foreach (var g in layer.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            return layer;
        }

        void Pillars()
        {
            var at = new[] { new Vector2(-9.5f, -8f), new Vector2(9.5f, -8f), new Vector2(-9.5f, 8.5f), new Vector2(9.5f, 8.5f) };
            int k = 0;
            foreach (var p in at)
            {
                var b = new Vector3(p.x, 0, p.y);
                marble.Cylinder(b, 0.5f, CeilingY, 16, Color.white, false);
                foreach (float y in new[] { 0.05f, 1.1f, 4.4f })
                    plain.Cylinder(b + Vector3.up * y, 0.56f, 0.16f, 16, Gold);
                Color neon = k % 2 == 0 ? Cyan : Pink;
                plain.Emission = 1.7f;
                plain.Anim = new Vector3(k, 0.4f, 1.5f);
                for (int s = 0; s < 4; s++)
                {
                    float a = s * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                    var o = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * 0.51f;
                    plain.Bar(b + o + Vector3.up * 1.3f, b + o + Vector3.up * 4.3f, 0.045f, neon);
                    glows.Add(b + o * 1.4f + Vector3.up * 2.8f, neon * 0.25f, 1.6f, k + s, 1.5f, 0.4f);
                }
                plain.Anim = Vector3.zero;
                plain.Emission = 0f;
                rig.Light(b + Vector3.up * 2.5f, neon, 3.2f, 0.45f);
                obstacles.Add(Obstacle.Round(p.x, p.y, 0.56f));
                k++;
            }
        }

        // ------------------------------------------------------------------ signs and bulbs

        /// <summary>A rectangle of chasing bulbs around a sign. <paramref name="facing"/> is the sign's outward normal.</summary>
        void Marquee(Vector3 center, float width, float height, Vector3 facing, Color color, float spacing = 0.18f)
        {
            Vector3 side = Vector3.Cross(Vector3.up, facing).normalized;
            Vector3 up = Vector3.up;
            var corners = new[]
            {
                center - side * width * 0.5f - up * height * 0.5f, center - side * width * 0.5f + up * height * 0.5f,
                center + side * width * 0.5f + up * height * 0.5f, center + side * width * 0.5f - up * height * 0.5f,
            };
            int bulb = 0;
            for (int e = 0; e < 4; e++)
            {
                Vector3 a = corners[e], b = corners[(e + 1) % 4];
                float len = (b - a).magnitude;
                int count = Mathf.Max(1, Mathf.RoundToInt(len / spacing));
                for (int i = 0; i < count; i++, bulb++)
                {
                    Vector3 p = Vector3.Lerp(a, b, i / (float)count) + facing * 0.03f;
                    float phase = -bulb * 2.1f;
                    plain.Emission = 2.2f;
                    plain.Anim = new Vector3(phase, 0.75f, 7f);
                    plain.Box(p - up * 0.025f, new Vector3(0.05f, 0.05f, 0.05f), Color.Lerp(color, Color.white, 0.5f));
                    plain.Anim = Vector3.zero;
                    plain.Emission = 0f;
                    glows.Add(p + facing * 0.03f, color * 0.5f, Mathf.Max(0.3f, spacing * 2.8f), phase, 7f, 0.85f);
                }
            }
        }

        /// <summary>Upright world-space text. <paramref name="yaw"/> is the direction the text faces away from (0 = readable from the south).</summary>
        void Sign(string text, Vector3 position, float yaw, float width, float height, Color color, FontKind font)
        {
            var label = SignLabel(text, width, height, color, font);
            label.transform.position = position;
            label.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        /// <summary>Text printed flat on a table, readable from the south.</summary>
        void SignFlat(string text, Vector3 position, float width, float height, Color color)
        {
            var label = SignLabel(text, width, height, color, FontKind.Display, false);
            label.transform.position = position;
            label.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        RectTransform SignLabel(string text, float width, float height, Color color, FontKind font, bool glow = true)
        {
            var go = new GameObject("Sign " + text, typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            const float ppm = 100f; // canvas units per metre
            rt.sizeDelta = new Vector2(width * ppm, height * ppm);
            rt.localScale = Vector3.one / ppm;
            var t = UIKit.Label(rt, text, Mathf.RoundToInt(height * ppm * 0.9f), color, TextAnchor.MiddleCenter, font);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 4;
            t.resizeTextMaxSize = Mathf.RoundToInt(height * ppm * 0.9f);
            ((RectTransform)t.transform).Stretch();
            if (glow) UIKit.TextOutline(t, Palette.WithAlpha(Palette.Shade(color, 0.5f), 0.8f), 1.5f);
            return rt;
        }

        // ------------------------------------------------------------------ staff

        readonly List<(Vector3 pos, float yaw, StaffRole role, int theme, StaffPose pose)> staffQueue = new List<(Vector3, float, StaffRole, int, StaffPose)>();

        /// <summary>Reserves a staff member here, facing <paramref name="yaw"/>; they're built later, one per frame. Returns the index.</summary>
        int SpawnStaff(Vector3 pos, float yaw, StaffRole role, int theme, StaffPose pose)
        {
            staffQueue.Add((pos, yaw, role, theme, pose));
            rig.Shadow(pos, new Vector2(0.22f, 0.15f), 0.5f);
            return staffQueue.Count - 1;
        }

        /// <summary>Which generated person (Resources/Staff) works at a post.</summary>
        static string ModelFor(bool female, StaffRole role, int theme)
        {
            string g = female ? "f_" : "m_";
            switch (role)
            {
                case StaffRole.Host: return g + "host";
                case StaffRole.Cashier: return g + "cashier";
                case StaffRole.Server: return g + "server";
                default: return g + (theme % 3 == 0 ? "dealer1" : theme % 3 == 1 ? "dealer3" : "dealer2");
            }
        }

        /// <summary>The room's light where someone stands, softened so faces stay bright.</summary>
        Color TintAt(Vector3 pos)
        {
            Color c = Color.Lerp(Color.white, rig.Evaluate(pos + Vector3.up * 1.4f, Vector3.up) * 0.8f, 0.35f);
            return new Color(Mathf.Clamp(c.r, 0.75f, 1.1f), Mathf.Clamp(c.g, 0.7f, 1.1f), Mathf.Clamp(c.b, 0.7f, 1.1f), 1f);
        }

        IEnumerator SpawnAllStaff()
        {
            var style = App.I.Staff;
            for (int i = 0; i < staffQueue.Count; i++)
            {
                var (pos, yaw, role, theme, pose) = staffQueue[i];
                bool female = Screens.Staff.IsFemale(style, role, theme);
                int seed = Screens.Staff.Seed(role, theme);
                var fig = StaffFigure.Create(transform, ModelFor(female, role, theme), pos, yaw, female, role,
                    Screens.Staff.Name(style, role, theme), TintAt(pos), pose, AvatarPainter.LookFor(female, seed, role));
                staff.Add(fig);
                yield return null;
            }
            foreach (var s in spots)
                if (s.StaffIndex >= 0) s.Staff = staff[s.StaffIndex];

            // The cocktail waitress walks a loop through the aisles with drinks.
            patrol.AddRange(new[] { new Vector2(0f, -4.4f), new Vector2(7.2f, -4.4f), new Vector2(7.2f, 2f), new Vector2(0f, 2f), new Vector2(-7.2f, 2f), new Vector2(-7.2f, -4.4f) });
            bool waitressFemale = Screens.Staff.IsFemale(style, StaffRole.Server, 1);
            waitress = StaffFigure.Create(transform, (waitressFemale ? "f_" : "m_") + "dealer2", new Vector3(patrol[0].x, 0, patrol[0].y), 90f,
                waitressFemale, StaffRole.Server, Screens.Staff.Name(style, StaffRole.Server, 1), new Color(1f, 0.95f, 0.92f), StaffPose.Tray,
                AvatarPainter.LookFor(waitressFemale, 53, StaffRole.Server));
            staff.Add(waitress);
            spots.Add(new Spot
            {
                Title = "ORDER A DRINK",
                Subtitle = waitress.StaffName + " brings it over",
                Icon = Icon.Food,
                Style = ButtonStyle.Purple,
                TapRadius = 0.7f,
                UseDistance = 2.4f,
                Use = () => App.I.UI.Modal<ShopModal>(m => m.StartTab = ShopTab.Food),
                Staff = waitress,
                FollowsStaff = true,
            });
        }
    }
}
