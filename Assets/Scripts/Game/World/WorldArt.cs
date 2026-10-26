using Casino.Art;
using UnityEngine;

namespace Casino.World
{
    /// <summary>
    /// Procedural textures for the 3D casino. Small, tiling, generated once at load. <c>detail</c> multiplies the
    /// resolution (the higher graphics settings use 2x or 4x): the patterns are drawn in texture space, so more pixels
    /// give crisper edges and finer veins and grain close up, not a different pattern.
    /// </summary>
    public static class WorldArt
    {
        static float Frac(float v) => v - Mathf.Floor(v);

        static float Hash(int x, int y)
        {
            uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663);
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            return (h & 0xFFFF) / 65535f;
        }

        static Texture2D Make(string name, int w, int h, System.Func<float, float, Color> pixel, bool mips = true)
        {
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = pixel((x + 0.5f) / w, (y + 0.5f) / h);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mips)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 2,
            };
            tex.SetPixels32(px);
            tex.Apply(mips, true);
            return tex;
        }

        static Color Smooth(Color a, Color b, float d, float width) => Color.Lerp(b, a, Mathf.Clamp01(0.5f - d / width));

        /// <summary>Busy, warm casino carpet: wine base, gold lattice, teal medallions, magenta stars. Tiles seamlessly.</summary>
        public static Texture2D Carpet(int detail = 1)
        {
            Color baseCol = Palette.Hex("4A0B2C"), deep = Palette.Hex("2E0619"), gold = Palette.Hex("E3A93A"),
                teal = Palette.Hex("16A6A0"), magenta = Palette.Hex("E0218A"), purple = Palette.Hex("6A2BA8");
            return Make("carpet", 256 * detail, 256 * detail, (x, y) =>
            {
                // diamond lattice (period 0.5)
                float u = (x + y) * 2f, v = (x - y) * 2f;
                float du = Mathf.Abs(Frac(u) - 0.5f), dv = Mathf.Abs(Frac(v) - 0.5f);
                float cell = Mathf.Min(du, dv);
                Color c = Color.Lerp(deep, baseCol, Mathf.Clamp01(cell * 3f));
                float lattice = 0.5f - Mathf.Max(du, dv);
                c = Smooth(gold, c, lattice - 0.012f, 0.012f);
                // medallion centred in each diamond
                float mx = Frac(x * 2f) - 0.5f, my = Frac(y * 2f) - 0.5f;
                float r = Mathf.Sqrt(mx * mx + my * my);
                float ang = Mathf.Atan2(my, mx);
                float petals = 0.13f + 0.05f * Mathf.Cos(ang * 4f);
                c = Smooth(teal, c, r - petals, 0.02f);
                c = Smooth(gold, c, Mathf.Abs(r - 0.1f) - 0.012f, 0.012f);
                c = Smooth(purple, c, r - 0.055f, 0.015f);
                c = Smooth(magenta, c, r - 0.025f, 0.012f);
                // tiny stars on the lattice crossings
                float sx = Frac(x * 2f + 0.5f) - 0.5f, sy = Frac(y * 2f + 0.5f) - 0.5f;
                float star = Mathf.Abs(sx) + Mathf.Abs(sy) * 0.35f;
                star = Mathf.Min(star, Mathf.Abs(sy) + Mathf.Abs(sx) * 0.35f);
                c = Smooth(magenta, c, star - 0.05f, 0.015f);
                float n = Hash((int)(x * 256 * detail), (int)(y * 256 * detail)) * 0.08f - 0.04f;   // pile, one speck per pixel
                return new Color(c.r + n, c.g + n, c.b + n, 1f);
            });
        }

        /// <summary>Burgundy damask wallpaper with a soft lighter ornament.</summary>
        public static Texture2D Damask(int detail = 1)
        {
            Color baseCol = Palette.Hex("5A0E22"), orn = Palette.Hex("7C1F38"), gold = Palette.Hex("B8862E");
            return Make("damask", 128 * detail, 256 * detail, (x, y) =>
            {
                Color c = baseCol;
                // two rows of ornaments, offset like a brick pattern
                for (int k = 0; k < 2; k++)
                {
                    float ox = Frac(x + k * 0.5f) - 0.5f, oy = Frac(y * 2f + k * 0.5f) - 0.5f;
                    float body = Mathf.Abs(ox) * 1.6f + Mathf.Abs(oy) * 0.9f - 0.22f;            // rhombus
                    float bulb = Mathf.Sqrt(ox * ox * 4f + (oy + 0.12f) * (oy + 0.12f) * 6f) - 0.2f; // flare
                    float d = Mathf.Min(body, bulb);
                    c = Smooth(orn, c, d, 0.03f);
                    c = Smooth(Color.Lerp(orn, gold, 0.35f), c, Mathf.Abs(d + 0.035f) - 0.006f, 0.008f);
                }
                float n = Hash((int)(x * 128 * detail), (int)(y * 256 * detail)) * 0.04f - 0.02f;
                return new Color(c.r + n, c.g + n, c.b + n, 1f);
            });
        }

        /// <summary>Polished mahogany.</summary>
        public static Texture2D Wood(int detail = 1)
        {
            Color a = Palette.Hex("3A140C"), b = Palette.Hex("6E2E18");
            return Make("wood", 128 * detail, 128 * detail, (x, y) =>
            {
                float grain = Mathf.Sin(y * 70f + Mathf.Sin(x * 9f) * 2.2f + Mathf.Sin(x * 31f) * 0.4f) * 0.5f + 0.5f;
                Color c = Color.Lerp(a, b, grain * 0.7f + Hash((int)(x * 16), (int)(y * 128)) * 0.15f);
                return c;
            });
        }

        /// <summary>Black marble tile with faint veins and a gold border (one tile per texture repeat).</summary>
        public static Texture2D Marble(int detail = 1)
        {
            Color dark = Palette.Hex("2C2236"), vein = Palette.Hex("6A5A7C"), gold = Palette.Hex("E8B54A");
            return Make("marble", 128 * detail, 128 * detail, (x, y) =>
            {
                float v = Mathf.Abs(Mathf.Sin((x * 3f + y * 1.4f) * 6f + Mathf.Sin(y * 13f) * 1.3f + Mathf.Sin(x * 23f) * 0.4f));
                Color c = Color.Lerp(vein, dark, Mathf.Clamp01(v * 6f));
                float edge = Mathf.Min(Mathf.Min(x, 1f - x), Mathf.Min(y, 1f - y));
                c = Smooth(gold, c, edge - 0.012f, 0.008f);
                return c;
            });
        }

        /// <summary>Back-bar shelves: three rows of backlit bottles (used with emission).</summary>
        public static Texture2D Bottles(int detail = 1)
        {
            Color[] glass = { Palette.Hex("F2A23A"), Palette.Hex("3FD08A"), Palette.Hex("4AA8FF"), Palette.Hex("FF5FA8"), Palette.Hex("F5E6C8"), Palette.Hex("B05BFF") };
            Color back = Palette.Hex("2A1240"), shelf = Palette.Hex("D9A23A");
            return Make("bottles", 256 * detail, 128 * detail, (x, y) =>
            {
                float row = y * 3f;
                int r = Mathf.FloorToInt(row);
                float ry = row - r;
                Color c = Color.Lerp(back, Palette.Hex("5A2A8A"), Mathf.Clamp01(1f - Mathf.Abs(ry - 0.55f) * 2f));
                if (ry < 0.06f) return shelf;
                int slot = Mathf.FloorToInt(x * 14f);
                float sx = x * 14f - slot - 0.5f;
                float hgt = 0.55f + Hash(slot, r) * 0.3f;
                float bodyW = 0.28f, neckW = 0.1f;
                float by = ry - 0.06f;
                bool body = Mathf.Abs(sx) < bodyW && by < hgt * 0.65f;
                bool neck = Mathf.Abs(sx) < neckW && by < hgt;
                if (body || neck)
                {
                    Color g = glass[(slot * 7 + r * 3) % glass.Length];
                    float shine = Mathf.Clamp01(1f - Mathf.Abs(sx + 0.12f) * 8f) * 0.5f;
                    c = Color.Lerp(g, Color.white, shine);
                    if (body && by > hgt * 0.25f && by < hgt * 0.45f && Mathf.Abs(sx) < bodyW * 0.8f) c = Palette.Hex("F7EBD0");
                }
                return c;
            });
        }

        /// <summary>Slot machine screen: three reels of symbols.</summary>
        public static Texture2D SlotReels(int detail = 1)
        {
            var c = new SdfCanvas(128 * detail, 128 * detail);
            c.Clear(Palette.Hex("1B0B2E"));
            for (int i = 0; i < 3; i++)
            {
                float cx = 0.19f + i * 0.31f;
                c.Fill(Sdf.Box(new Vector2(cx, 0.5f), new Vector2(0.13f, 0.4f), 0.03f), Palette.Hex("FFF6E0"));
                // three symbols per reel: seven, cherry, bar / diamond
                for (int k = 0; k < 3; k++)
                {
                    float cy = 0.24f + k * 0.26f;
                    int sym = (i * 2 + k * 3) % 4;
                    var at = new Vector2(cx, cy);
                    switch (sym)
                    {
                        case 0: // seven
                            c.Fill(Sdf.Segment(at + new Vector2(-0.05f, 0.06f), at + new Vector2(0.05f, 0.06f), 0.016f), Palette.Hex("E2183A"));
                            c.Fill(Sdf.Segment(at + new Vector2(0.05f, 0.06f), at + new Vector2(-0.015f, -0.07f), 0.016f), Palette.Hex("E2183A"));
                            break;
                        case 1: // cherries
                            c.Fill(Sdf.Circle(at + new Vector2(-0.03f, -0.03f), 0.032f), Palette.Hex("D0103A"));
                            c.Fill(Sdf.Circle(at + new Vector2(0.035f, -0.04f), 0.032f), Palette.Hex("D0103A"));
                            c.Fill(Sdf.Segment(at + new Vector2(-0.03f, 0f), at + new Vector2(0.02f, 0.07f), 0.006f), Palette.Hex("2E8B3A"));
                            c.Fill(Sdf.Segment(at + new Vector2(0.035f, -0.01f), at + new Vector2(0.02f, 0.07f), 0.006f), Palette.Hex("2E8B3A"));
                            break;
                        case 2: // BAR
                            c.Fill(Sdf.Box(at, new Vector2(0.09f, 0.035f), 0.01f), Palette.Hex("17141C"));
                            c.Fill(Sdf.Box(at, new Vector2(0.075f, 0.02f), 0.006f), Palette.Hex("F5C542"));
                            break;
                        default: // diamond
                            c.Fill(Sdf.Rhombus(at, new Vector2(0.06f, 0.075f)), Palette.Hex("35C8F0"));
                            c.Fill(Sdf.Rhombus(at + new Vector2(-0.012f, 0.015f), new Vector2(0.025f, 0.03f)), Palette.Hex("C8F6FF"));
                            break;
                    }
                }
            }
            c.Fill(Sdf.Box(new Vector2(0.5f, 0.5f), new Vector2(0.48f, 0.006f)), Palette.Hex("FF2E63"));
            return c.ToTexture("slotreels", FilterMode.Bilinear, true);
        }

        /// <summary>
        /// A video poker screen for the machines in the room: a paytable band over five dealt cards (a royal flush in
        /// spades, two of them marked HELD). Twice as wide as tall, like the panel it lies on.
        /// </summary>
        public static Texture2D PokerScreen(int detail = 1)
        {
            var c = new SdfCanvas(128 * detail, 64 * detail);   // x spans 0..1, y 0..0.5
            c.Clear(Palette.Hex("0B2A5E"));
            // the paytable: two columns of gold rows
            for (int row = 0; row < 4; row++)
            {
                float y = 0.455f - row * 0.03f;
                c.Fill(Sdf.Box(new Vector2(0.26f, y), new Vector2(0.2f, 0.007f), 0.003f), Palette.WithAlpha(Palette.GoldLight, 0.75f));
                c.Fill(Sdf.Box(new Vector2(0.74f, y), new Vector2(0.2f, 0.007f), 0.003f), Palette.WithAlpha(Palette.GoldLight, 0.75f));
            }
            c.Fill(Sdf.Box(new Vector2(0.5f, 0.335f), new Vector2(0.47f, 0.004f)), Palette.Hex("F5C542"));
            for (int i = 0; i < 5; i++)
            {
                var at = new Vector2(0.12f + i * 0.19f, 0.18f);
                c.Fill(Sdf.Box(at, new Vector2(0.075f, 0.11f), 0.012f), Palette.Hex("FFFDF6"));
                c.Fill(Painters.SuitShape(Casino.Core.Blackjack.Suit.Spades, at, 0.09f), Palette.Hex("17141C"));
                if (i == 0 || i == 1) c.Fill(Sdf.Box(new Vector2(at.x, 0.035f), new Vector2(0.06f, 0.012f), 0.004f), Palette.Hex("F5C542"));
            }
            return c.ToTexture("pokerscreen", FilterMode.Bilinear, true);
        }

        /// <summary>Soft radial glow for the additive light sprites.</summary>
        public static Texture2D Glow()
        {
            var tex = Painters.Glow(64).ToTexture("glow", FilterMode.Bilinear, true);
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }
    }
}
