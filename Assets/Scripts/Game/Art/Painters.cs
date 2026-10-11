using Casino.Core.Blackjack;
using UnityEngine;

namespace Casino.Art
{
    public enum Icon
    {
        Coin, Bolt, Gear, Plus, Back, Close, Lock, Star, Play, Check, Music, Speaker, Globe, Gift, Clock, Crown,
        Cards, Person, Video, Chips, Bulb, Sparkle, Food, Exit, Flame, Freeze, Target,
        PadCross, PadCircle, PadSquare, PadTriangle,
    }

    /// <summary>Pure painting routines. Each returns a canvas; <see cref="ProceduralArt"/> turns them into sprites.</summary>
    public static class Painters
    {
        static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        // ---------------- UI primitives ----------------

        /// <summary>White rounded rectangle, 64px with a 28px corner, meant for 9-slicing (border 30).</summary>
        public static SdfCanvas RoundRect()
        {
            var c = new SdfCanvas(64, 64);
            float m = 0.5f / 64f;
            c.Fill(Sdf.Box(Center, new Vector2(0.5f - m, 0.5f - m), 28f / 64f), Color.white);
            return c;
        }

        public static SdfCanvas RoundRectOutline(float thicknessPx)
        {
            var c = new SdfCanvas(64, 64);
            float m = 0.5f / 64f, t = thicknessPx / 64f;
            var outer = Sdf.Box(Center, new Vector2(0.5f - m, 0.5f - m), 28f / 64f);
            c.Fill(Sdf.Subtract(outer, Sdf.Grow(outer, -t)), Color.white);
            return c;
        }

        public static SdfCanvas SoftShadow()
        {
            var c = new SdfCanvas(64, 64);
            c.Fill(Sdf.Box(Center, new Vector2(0.28f, 0.28f), 0.1f), new Color(0, 0, 0, 1f), 16f);
            return c;
        }

        public static SdfCanvas Circle(int size = 128)
        {
            var c = new SdfCanvas(size, size);
            c.Fill(Sdf.Circle(Center, 0.5f - 1f / size), Color.white);
            return c;
        }

        public static SdfCanvas Ring(float thickness, int size = 128)
        {
            var c = new SdfCanvas(size, size);
            c.Fill(Sdf.Ring(Center, 0.5f - thickness - 1f / size, thickness), Color.white);
            return c;
        }

        public static SdfCanvas DashedRing(int dashes, float thickness, int size = 256)
        {
            var c = new SdfCanvas(size, size);
            float r = 0.5f - thickness - 2f / size;
            c.Paint(Sdf.Ring(Center, r, thickness), p =>
            {
                float a = Mathf.Atan2(p.y - 0.5f, p.x - 0.5f) / (2f * Mathf.PI) + 0.5f;
                float phase = a * dashes - Mathf.Floor(a * dashes);
                return new Color(1, 1, 1, phase < 0.6f ? 1f : 0.15f);
            });
            return c;
        }

        public static SdfCanvas Glow(int size = 128)
        {
            var c = new SdfCanvas(size, size);
            c.Paint(Sdf.Circle(Center, 0.5f), p =>
            {
                float d = Mathf.Clamp01((p - Center).magnitude * 2f);
                float a = (1f - d) * (1f - d);
                return new Color(1, 1, 1, a);
            });
            return c;
        }

        /// <summary>Vertical alpha ramp: opaque top, transparent bottom.</summary>
        public static SdfCanvas GradientV()
        {
            var c = new SdfCanvas(8, 64);
            c.Paint(_ => -1f, p => new Color(1, 1, 1, Mathf.SmoothStep(0f, 1f, p.y)));
            return c;
        }

        public static SdfCanvas VerticalGradient(Color top, Color bottom, int height = 256)
        {
            var c = new SdfCanvas(4, height);
            c.Paint(_ => -1f, p => Palette.Mix(bottom, top, p.y));
            return c;
        }

        public static SdfCanvas Vignette(float strength)
        {
            var c = new SdfCanvas(128, 128);
            c.Paint(_ => -1f, p =>
            {
                float d = (p - Center).magnitude / 0.7071f;
                return new Color(0, 0, 0, Smooth(0.45f, 1.05f, d) * strength);
            });
            return c;
        }

        /// <summary>Radial felt with subtle noise: brighter under the lights, darker at the rail.</summary>
        public static SdfCanvas Felt(Color felt)
        {
            var c = new SdfCanvas(256, 256);
            Color hi = Palette.Shade(felt, 1.35f), lo = Palette.Shade(felt, 0.45f);
            c.Paint(_ => -1f, p =>
            {
                float d = Mathf.Clamp01(new Vector2((p.x - 0.5f) * 1.1f, (p.y - 0.62f) * 1.5f).magnitude * 1.25f);
                Color col = Palette.Mix(hi, lo, d * d);
                float n = Hash(p.x * 256f, p.y * 256f) * 0.05f - 0.025f;
                return new Color(col.r + n, col.g + n, col.b + n, 1f);
            });
            return c;
        }

        public static SdfCanvas Sunburst(int rays = 18)
        {
            var c = new SdfCanvas(256, 256);
            c.Paint(_ => -1f, p =>
            {
                Vector2 d = p - Center;
                float ang = Mathf.Atan2(d.y, d.x) / (2f * Mathf.PI) * rays;
                float band = ang - Mathf.Floor(ang);
                float ray = Smooth(0.35f, 0.5f, band) * (1f - Smooth(0.85f, 1f, band));
                float fade = 1f - Mathf.Clamp01(d.magnitude * 2f);
                return new Color(1, 1, 1, ray * fade * fade * 0.9f);
            });
            return c;
        }

        public static SdfCanvas Sparkle()
        {
            var c = new SdfCanvas(64, 64);
            c.Paint(Sdf.Circle(Center, 0.5f), p =>
            {
                float d = (p - Center).magnitude * 2f;
                return new Color(1, 1, 1, Mathf.Clamp01(1f - d) * 0.35f);
            });
            c.Fill(Sdf.Union(Sdf.Rhombus(Center, new Vector2(0.07f, 0.46f)), Sdf.Rhombus(Center, new Vector2(0.46f, 0.07f))), Color.white, 1.5f);
            return c;
        }

        public static SdfCanvas Bulb()
        {
            var c = new SdfCanvas(48, 48);
            c.Paint(Sdf.Circle(Center, 0.5f), p => new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1f - (p - Center).magnitude * 2f), 1.6f) * 0.8f));
            c.Fill(Sdf.Circle(Center, 0.18f), Color.white);
            return c;
        }

        public static SdfCanvas BubbleTail()
        {
            var c = new SdfCanvas(64, 64);
            c.Fill(Sdf.Triangle(new Vector2(0.05f, 0.98f), new Vector2(0.95f, 0.98f), new Vector2(0.2f, 0.05f)), Color.white);
            return c;
        }

        // ---------------- cards ----------------

        /// <summary>Card face base for 9-slicing: white with a thin grey edge (64px, border 16).</summary>
        public static SdfCanvas CardFace()
        {
            var c = new SdfCanvas(64, 64);
            var outer = Sdf.Box(Center, new Vector2(0.5f - 0.5f / 64f, 0.5f - 0.5f / 64f), 13f / 64f);
            c.Fill(outer, Palette.Hex("BDB6C8"));
            c.Fill(Sdf.Grow(outer, -1.6f / 64f), Palette.Hex("FFFDF8"));
            return c;
        }

        public static SdfCanvas CardBack(Color main)
        {
            const int w = 140, h = 196;
            var c = new SdfCanvas(w, h);
            float W = w / (float)h, H = 1f; // canvas units: the longer side is 1
            Vector2 mid = new Vector2(W / 2f, H / 2f);
            var outer = Sdf.Box(mid, new Vector2(W / 2f - 0.004f, H / 2f - 0.004f), 0.06f);
            c.Fill(outer, Palette.Hex("BDB6C8"));
            c.Fill(Sdf.Grow(outer, -0.008f), Color.white);
            var inner = Sdf.Box(mid, new Vector2(W / 2f - 0.045f, H / 2f - 0.045f), 0.03f);
            Color dark = Palette.Shade(main, 0.72f);
            c.Paint(inner, p =>
            {
                // diamond lattice
                float u = (p.x + p.y) * 22f, v = (p.x - p.y) * 22f;
                float fu = u - Mathf.Floor(u), fv = v - Mathf.Floor(v);
                bool line = fu < 0.16f || fv < 0.16f;
                return line ? Palette.Shade(main, 1.25f) : ((Mathf.FloorToInt(u) + Mathf.FloorToInt(v)) % 2 == 0 ? main : dark);
            });
            c.Fill(Sdf.Outline(inner, 0.004f), Palette.Gold);
            c.Fill(Sdf.Circle(mid, 0.135f), Palette.GoldDark);
            c.Fill(Sdf.Circle(mid, 0.12f), Palette.Shade(main, 0.6f));
            c.Fill(Sdf.Ring(mid, 0.1f, 0.006f), Palette.Gold);
            c.Fill(SpadeShape(mid, 0.15f), Palette.Gold);
            return c;
        }

        public static SdfCanvas Suit(Suit suit)
        {
            var c = new SdfCanvas(128, 128);
            c.Fill(SuitShape(suit, Center, 0.88f), Color.white);
            return c;
        }

        public static SdfShape SuitShape(Suit suit, Vector2 c, float size)
        {
            switch (suit)
            {
                case Casino.Core.Blackjack.Suit.Hearts:
                    return Sdf.Heart(new Vector2(c.x, c.y - 0.47f * size), 0.8f * size);
                case Casino.Core.Blackjack.Suit.Diamonds:
                    return Sdf.Rhombus(c, new Vector2(0.38f * size, 0.5f * size));
                case Casino.Core.Blackjack.Suit.Clubs:
                {
                    float r = 0.2f * size;
                    return Sdf.Union(
                        Sdf.Circle(new Vector2(c.x, c.y + 0.21f * size), r),
                        Sdf.Circle(new Vector2(c.x - 0.215f * size, c.y - 0.08f * size), r),
                        Sdf.Circle(new Vector2(c.x + 0.215f * size, c.y - 0.08f * size), r),
                        Sdf.Circle(new Vector2(c.x, c.y + 0.02f * size), r * 0.6f),
                        Stem(c, size));
                }
                default:
                    return SpadeShape(c, size);
            }
        }

        static SdfShape SpadeShape(Vector2 c, float size) => Sdf.Union(
            Sdf.Heart(new Vector2(c.x, c.y + 0.5f * size), 0.72f * size, true),
            Stem(c, size));

        static SdfShape Stem(Vector2 c, float size) => Sdf.Triangle(
            new Vector2(c.x, c.y - 0.02f * size),
            new Vector2(c.x - 0.17f * size, c.y - 0.48f * size),
            new Vector2(c.x + 0.17f * size, c.y - 0.48f * size));

        // ---------------- chips ----------------

        public static SdfCanvas Chip(Color color)
        {
            var c = new SdfCanvas(128, 128);
            c.Fill(Sdf.Circle(Center, 0.49f), Palette.Shade(color, 0.55f));
            c.Fill(Sdf.Circle(Center, 0.475f), color);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f;
                Vector2 pos = Center + new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)) * 0.405f;
                c.Fill(Sdf.Rotate(Sdf.Box(pos, new Vector2(0.075f, 0.05f), 0.015f), pos, a + 90f), new Color(1, 1, 1, 0.95f));
            }
            c.Fill(Sdf.Ring(Center, 0.31f, 0.012f), new Color(1, 1, 1, 0.85f));
            c.Fill(Sdf.Circle(Center, 0.285f), Palette.Shade(color, 0.85f));
            c.Paint(Sdf.Circle(Center, 0.475f), p => new Color(1, 1, 1, Mathf.Clamp01((p.y - 0.45f) * 0.35f)));
            return c;
        }

        // ---------------- icons (white, tinted in UI) ----------------

        public static SdfCanvas IconImage(Icon icon)
        {
            var c = new SdfCanvas(128, 128);
            Color w = Color.white;
            switch (icon)
            {
                case Icon.Coin:
                    c.Fill(Sdf.Circle(Center, 0.47f), Palette.GoldDark);
                    c.Fill(Sdf.Circle(Center, 0.42f), Palette.Gold);
                    c.Fill(Sdf.Ring(Center, 0.32f, 0.025f), Palette.GoldDark);
                    c.Fill(Sdf.Star5(Center, 0.2f, 0.45f), Palette.GoldLight);
                    c.Paint(Sdf.Circle(Center, 0.42f), p => new Color(1, 1, 1, Mathf.Clamp01((p.y - 0.55f) * 0.6f)));
                    break;
                case Icon.Bolt:
                    c.Fill(Sdf.Polygon(new Vector2(0.6f, 0.96f), new Vector2(0.2f, 0.44f), new Vector2(0.46f, 0.44f),
                        new Vector2(0.38f, 0.04f), new Vector2(0.8f, 0.58f), new Vector2(0.53f, 0.58f)), w);
                    break;
                case Icon.Gear:
                {
                    SdfShape gear = Sdf.Circle(Center, 0.3f);
                    for (int i = 0; i < 8; i++)
                        gear = Sdf.Union(gear, Sdf.Rotate(Sdf.Box(new Vector2(0.5f, 0.82f), new Vector2(0.075f, 0.1f), 0.02f), Center, i * 45f));
                    c.Fill(Sdf.Subtract(gear, Sdf.Circle(Center, 0.13f)), w);
                    break;
                }
                case Icon.Plus:
                    c.Fill(Sdf.Union(Sdf.Box(Center, new Vector2(0.08f, 0.36f), 0.05f), Sdf.Box(Center, new Vector2(0.36f, 0.08f), 0.05f)), w);
                    break;
                case Icon.Back:
                    c.Fill(Sdf.Union(Sdf.Segment(new Vector2(0.64f, 0.85f), new Vector2(0.3f, 0.5f), 0.075f),
                        Sdf.Segment(new Vector2(0.3f, 0.5f), new Vector2(0.64f, 0.15f), 0.075f)), w);
                    break;
                case Icon.Close:
                    c.Fill(Sdf.Union(Sdf.Segment(new Vector2(0.22f, 0.22f), new Vector2(0.78f, 0.78f), 0.07f),
                        Sdf.Segment(new Vector2(0.22f, 0.78f), new Vector2(0.78f, 0.22f), 0.07f)), w);
                    break;
                case Icon.Lock:
                    c.Fill(Sdf.Arc(new Vector2(0.5f, 0.58f), 0.19f, 0.055f, 90f, 90f), w);
                    c.Fill(Sdf.Subtract(Sdf.Box(new Vector2(0.5f, 0.34f), new Vector2(0.3f, 0.24f), 0.06f),
                        Sdf.Union(Sdf.Circle(new Vector2(0.5f, 0.38f), 0.06f), Sdf.Box(new Vector2(0.5f, 0.27f), new Vector2(0.025f, 0.08f)))), w);
                    break;
                case Icon.Star:
                    c.Fill(Sdf.Star5(new Vector2(0.5f, 0.47f), 0.46f, 0.48f), w);
                    break;
                case Icon.Play:
                    c.Fill(Sdf.Grow(Sdf.Triangle(new Vector2(0.3f, 0.18f), new Vector2(0.3f, 0.82f), new Vector2(0.84f, 0.5f)), -0.03f), w, 1f);
                    break;
                case Icon.Check:
                    c.Fill(Sdf.Union(Sdf.Segment(new Vector2(0.18f, 0.52f), new Vector2(0.42f, 0.28f), 0.075f),
                        Sdf.Segment(new Vector2(0.42f, 0.28f), new Vector2(0.84f, 0.76f), 0.075f)), w);
                    break;
                case Icon.Music:
                    c.Fill(Sdf.Union(
                        Sdf.Rotate(Sdf.Ellipse(new Vector2(0.3f, 0.24f), new Vector2(0.14f, 0.1f)), new Vector2(0.3f, 0.24f), 20f),
                        Sdf.Rotate(Sdf.Ellipse(new Vector2(0.72f, 0.32f), new Vector2(0.14f, 0.1f)), new Vector2(0.72f, 0.32f), 20f),
                        Sdf.Box(new Vector2(0.415f, 0.52f), new Vector2(0.035f, 0.3f)),
                        Sdf.Box(new Vector2(0.835f, 0.6f), new Vector2(0.035f, 0.3f)),
                        Sdf.Polygon(new Vector2(0.38f, 0.78f), new Vector2(0.87f, 0.9f), new Vector2(0.87f, 0.74f), new Vector2(0.38f, 0.62f))), w);
                    break;
                case Icon.Speaker:
                    c.Fill(Sdf.Polygon(new Vector2(0.1f, 0.38f), new Vector2(0.28f, 0.38f), new Vector2(0.5f, 0.18f),
                        new Vector2(0.5f, 0.82f), new Vector2(0.28f, 0.62f), new Vector2(0.1f, 0.62f)), w);
                    c.Fill(Sdf.Arc(new Vector2(0.5f, 0.5f), 0.17f, 0.035f, 0f, 50f), w);
                    c.Fill(Sdf.Arc(new Vector2(0.5f, 0.5f), 0.32f, 0.035f, 0f, 50f), w);
                    break;
                case Icon.Globe:
                    c.Fill(Sdf.Ring(Center, 0.4f, 0.04f), w);
                    c.Fill(Sdf.Outline(Sdf.Ellipse(Center, new Vector2(0.17f, 0.4f)), 0.035f), w);
                    c.Fill(Sdf.Box(Center, new Vector2(0.4f, 0.035f)), w);
                    c.Fill(Sdf.Intersect(Sdf.Box(new Vector2(0.5f, 0.7f), new Vector2(0.4f, 0.03f)), Sdf.Circle(Center, 0.4f)), w);
                    c.Fill(Sdf.Intersect(Sdf.Box(new Vector2(0.5f, 0.3f), new Vector2(0.4f, 0.03f)), Sdf.Circle(Center, 0.4f)), w);
                    break;
                case Icon.Gift:
                    c.Fill(Sdf.Box(new Vector2(0.5f, 0.34f), new Vector2(0.33f, 0.24f), 0.03f), w);
                    c.Fill(Sdf.Box(new Vector2(0.5f, 0.62f), new Vector2(0.38f, 0.07f), 0.03f), w);
                    c.Fill(Sdf.Outline(Sdf.Ellipse(new Vector2(0.38f, 0.78f), new Vector2(0.12f, 0.08f)), 0.035f), w);
                    c.Fill(Sdf.Outline(Sdf.Ellipse(new Vector2(0.62f, 0.78f), new Vector2(0.12f, 0.08f)), 0.035f), w);
                    c.Fill(Sdf.Box(new Vector2(0.5f, 0.45f), new Vector2(0.045f, 0.35f)), new Color(0, 0, 0, 0.35f));
                    break;
                case Icon.Clock:
                    c.Fill(Sdf.Ring(Center, 0.4f, 0.05f), w);
                    c.Fill(Sdf.Union(Sdf.Segment(Center, new Vector2(0.5f, 0.76f), 0.04f), Sdf.Segment(Center, new Vector2(0.68f, 0.42f), 0.04f)), w);
                    break;
                case Icon.Crown:
                    c.Fill(Sdf.Polygon(new Vector2(0.12f, 0.25f), new Vector2(0.88f, 0.25f), new Vector2(0.92f, 0.72f), new Vector2(0.7f, 0.5f),
                        new Vector2(0.5f, 0.82f), new Vector2(0.3f, 0.5f), new Vector2(0.08f, 0.72f)), w);
                    c.Fill(Sdf.Box(new Vector2(0.5f, 0.17f), new Vector2(0.38f, 0.05f), 0.02f), w);
                    break;
                case Icon.Cards:
                {
                    var back = Sdf.Rotate(Sdf.Box(new Vector2(0.42f, 0.52f), new Vector2(0.2f, 0.3f), 0.04f), new Vector2(0.42f, 0.52f), 14f);
                    var front = Sdf.Rotate(Sdf.Box(new Vector2(0.58f, 0.48f), new Vector2(0.2f, 0.3f), 0.04f), new Vector2(0.58f, 0.48f), -10f);
                    c.Fill(Sdf.Subtract(back, Sdf.Grow(front, 0.04f)), w);
                    c.Fill(front, w);
                    c.Fill(SpadeShape(new Vector2(0.58f, 0.5f), 0.22f), new Color(0, 0, 0, 0.6f));
                    break;
                }
                case Icon.Person:
                    c.Fill(Sdf.Circle(new Vector2(0.5f, 0.68f), 0.17f), w);
                    c.Fill(Sdf.Intersect(Sdf.Ellipse(new Vector2(0.5f, 0.12f), new Vector2(0.32f, 0.32f)), Sdf.HalfPlaneAbove(0.08f)), w);
                    break;
                case Icon.Video:
                    c.Fill(Sdf.Subtract(Sdf.Box(Center, new Vector2(0.4f, 0.29f), 0.08f),
                        Sdf.Triangle(new Vector2(0.42f, 0.34f), new Vector2(0.42f, 0.66f), new Vector2(0.65f, 0.5f))), w);
                    break;
                case Icon.Chips:
                    for (int i = 0; i < 4; i++)
                    {
                        float y = 0.24f + i * 0.13f;
                        c.Fill(Sdf.Ellipse(new Vector2(0.5f, y), new Vector2(0.36f, 0.12f)), new Color(0, 0, 0, 1f));
                        c.Fill(Sdf.Ellipse(new Vector2(0.5f, y + 0.03f), new Vector2(0.36f, 0.12f)), w);
                    }
                    break;
                case Icon.Bulb:
                    c.Fill(Sdf.Union(Sdf.Circle(new Vector2(0.5f, 0.6f), 0.27f), Sdf.Box(new Vector2(0.5f, 0.3f), new Vector2(0.12f, 0.12f), 0.03f)), w);
                    break;
                case Icon.Sparkle:
                    c.Fill(Sdf.Union(Sdf.Rhombus(Center, new Vector2(0.12f, 0.46f)), Sdf.Rhombus(Center, new Vector2(0.46f, 0.12f))), w);
                    break;
                case Icon.Food:
                    // fork and knife
                    c.Fill(Sdf.Union(Sdf.Box(new Vector2(0.34f, 0.36f), new Vector2(0.035f, 0.28f), 0.03f),
                        Sdf.Box(new Vector2(0.34f, 0.74f), new Vector2(0.12f, 0.06f), 0.03f),
                        Sdf.Box(new Vector2(0.25f, 0.82f), new Vector2(0.025f, 0.1f), 0.02f),
                        Sdf.Box(new Vector2(0.34f, 0.82f), new Vector2(0.025f, 0.1f), 0.02f),
                        Sdf.Box(new Vector2(0.43f, 0.82f), new Vector2(0.025f, 0.1f), 0.02f)), w);
                    c.Fill(Sdf.Union(Sdf.Box(new Vector2(0.67f, 0.36f), new Vector2(0.035f, 0.28f), 0.03f),
                        Sdf.Ellipse(new Vector2(0.69f, 0.72f), new Vector2(0.07f, 0.2f))), w);
                    break;
                case Icon.Flame:
                {
                    // Outer flame with a darker inner tongue, so one tint reads as a two-tone flame.
                    var outer = Sdf.SmoothUnion(Sdf.Circle(new Vector2(0.5f, 0.34f), 0.27f),
                        Sdf.Triangle(new Vector2(0.25f, 0.42f), new Vector2(0.75f, 0.42f), new Vector2(0.56f, 0.95f)), 0.1f);
                    outer = Sdf.SmoothUnion(outer, Sdf.Triangle(new Vector2(0.6f, 0.5f), new Vector2(0.8f, 0.48f), new Vector2(0.78f, 0.74f)), 0.06f);
                    c.Fill(outer, w);
                    var inner = Sdf.SmoothUnion(Sdf.Circle(new Vector2(0.5f, 0.28f), 0.13f),
                        Sdf.Triangle(new Vector2(0.38f, 0.32f), new Vector2(0.62f, 0.32f), new Vector2(0.48f, 0.62f)), 0.06f);
                    c.Fill(inner, new Color(0.25f, 0f, 0f, 0.4f));
                    break;
                }
                case Icon.Freeze:
                    // Snowflake: three bars through the centre with a small V near each tip.
                    for (int k = 0; k < 6; k++)
                    {
                        float a = k * 60f * Mathf.Deg2Rad;
                        var dir = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
                        var side = new Vector2(dir.y, -dir.x);
                        c.Fill(Sdf.Segment(Center, Center + dir * 0.4f, 0.035f), w);
                        Vector2 b = Center + dir * 0.26f;
                        c.Fill(Sdf.Union(Sdf.Segment(b, b + (dir + side) * 0.09f, 0.03f), Sdf.Segment(b, b + (dir - side) * 0.09f, 0.03f)), w);
                    }
                    break;
                case Icon.Target:
                    c.Fill(Sdf.Union(Sdf.Ring(Center, 0.38f, 0.045f), Sdf.Ring(Center, 0.22f, 0.045f), Sdf.Circle(Center, 0.08f)), w);
                    break;
                // PlayStation face buttons, drawn as outlines like on the pad.
                case Icon.PadCross:
                    c.Fill(Sdf.Union(Sdf.Segment(new Vector2(0.24f, 0.24f), new Vector2(0.76f, 0.76f), 0.05f),
                        Sdf.Segment(new Vector2(0.24f, 0.76f), new Vector2(0.76f, 0.24f), 0.05f)), w);
                    break;
                case Icon.PadCircle:
                    c.Fill(Sdf.Ring(Center, 0.3f, 0.05f), w);
                    break;
                case Icon.PadSquare:
                    c.Fill(Sdf.Outline(Sdf.Box(Center, new Vector2(0.27f, 0.27f), 0.02f), 0.05f), w);
                    break;
                case Icon.PadTriangle:
                    c.Fill(Sdf.Outline(Sdf.Triangle(new Vector2(0.2f, 0.27f), new Vector2(0.8f, 0.27f), new Vector2(0.5f, 0.8f)), 0.05f), w);
                    break;
                case Icon.Exit:
                    c.Fill(Sdf.Subtract(Sdf.Outline(Sdf.Box(new Vector2(0.38f, 0.5f), new Vector2(0.24f, 0.36f), 0.05f), 0.04f),
                        Sdf.Box(new Vector2(0.62f, 0.5f), new Vector2(0.06f, 0.16f))), w);
                    c.Fill(Sdf.Union(Sdf.Segment(new Vector2(0.42f, 0.5f), new Vector2(0.86f, 0.5f), 0.045f),
                        Sdf.Segment(new Vector2(0.86f, 0.5f), new Vector2(0.72f, 0.64f), 0.045f),
                        Sdf.Segment(new Vector2(0.86f, 0.5f), new Vector2(0.72f, 0.36f), 0.045f)), w);
                    break;
            }
            return c;
        }

        // ---------------- food (full colour) ----------------

        public static SdfCanvas FoodImage(string productId)
        {
            var c = new SdfCanvas(128, 128);
            switch (productId)
            {
                case "food_water":
                {
                    var glass = Sdf.Polygon(new Vector2(0.27f, 0.88f), new Vector2(0.73f, 0.88f), new Vector2(0.65f, 0.08f), new Vector2(0.35f, 0.08f));
                    c.Fill(glass, new Color(0.85f, 0.95f, 1f, 0.35f));
                    c.Fill(Sdf.Intersect(glass, Sdf.HalfPlaneBelow(0.62f)), Palette.Hex("4FC3F7"));
                    c.Fill(Sdf.Intersect(glass, Sdf.Box(new Vector2(0.5f, 0.615f), new Vector2(0.4f, 0.012f))), Palette.Hex("B3E5FC"));
                    foreach (var b in new[] { new Vector2(0.45f, 0.3f), new Vector2(0.56f, 0.42f), new Vector2(0.5f, 0.52f), new Vector2(0.58f, 0.22f) })
                        c.Fill(Sdf.Circle(b, 0.022f), new Color(1, 1, 1, 0.8f));
                    c.Fill(Sdf.Outline(glass, 0.012f), new Color(1, 1, 1, 0.9f));
                    c.Fill(Sdf.Circle(new Vector2(0.72f, 0.84f), 0.13f), Palette.Hex("7CB342"));
                    c.Fill(Sdf.Circle(new Vector2(0.72f, 0.84f), 0.105f), Palette.Hex("DCEDC8"));
                    for (int i = 0; i < 4; i++)
                        c.Fill(Sdf.Rotate(Sdf.Box(new Vector2(0.72f, 0.84f), new Vector2(0.1f, 0.006f)), new Vector2(0.72f, 0.84f), i * 45f), Palette.Hex("9CCC65"));
                    break;
                }
                case "food_espresso":
                    c.Fill(Sdf.Ellipse(new Vector2(0.48f, 0.16f), new Vector2(0.4f, 0.08f)), Palette.Hex("ECEFF1"));
                    c.Fill(Sdf.Ellipse(new Vector2(0.48f, 0.16f), new Vector2(0.24f, 0.04f)), Palette.Hex("CFD8DC"));
                    c.Fill(Sdf.Ring(new Vector2(0.73f, 0.4f), 0.09f, 0.03f), Palette.Hex("FAFAFA"));
                    c.Fill(Sdf.Union(Sdf.Box(new Vector2(0.48f, 0.42f), new Vector2(0.24f, 0.18f), 0.02f),
                        Sdf.Ellipse(new Vector2(0.48f, 0.26f), new Vector2(0.24f, 0.07f))), Palette.Hex("FAFAFA"));
                    c.Fill(Sdf.Ellipse(new Vector2(0.48f, 0.6f), new Vector2(0.24f, 0.06f)), Palette.Hex("B0BEC5"));
                    c.Fill(Sdf.Ellipse(new Vector2(0.48f, 0.6f), new Vector2(0.21f, 0.045f)), Palette.Hex("5D4037"));
                    c.Fill(Sdf.Ellipse(new Vector2(0.46f, 0.605f), new Vector2(0.12f, 0.02f)), Palette.Hex("A1887F"));
                    c.Fill(Sdf.Arc(new Vector2(0.4f, 0.75f), 0.06f, 0.014f, 180f, 70f), new Color(1, 1, 1, 0.7f));
                    c.Fill(Sdf.Arc(new Vector2(0.4f, 0.87f), 0.06f, 0.014f, 0f, 70f), new Color(1, 1, 1, 0.7f));
                    c.Fill(Sdf.Arc(new Vector2(0.56f, 0.78f), 0.06f, 0.014f, 180f, 70f), new Color(1, 1, 1, 0.55f));
                    c.Fill(Sdf.Arc(new Vector2(0.56f, 0.9f), 0.06f, 0.014f, 0f, 70f), new Color(1, 1, 1, 0.55f));
                    break;
                case "food_energy":
                {
                    var can = Sdf.Box(new Vector2(0.5f, 0.47f), new Vector2(0.21f, 0.38f), 0.07f);
                    c.Fill(can, Palette.Hex("1B5E20"));
                    c.Paint(can, p => new Color(1, 1, 1, Mathf.Clamp01(0.35f - Mathf.Abs(p.x - 0.42f) * 3f)));
                    c.Fill(Sdf.Intersect(can, Sdf.Box(new Vector2(0.5f, 0.86f), new Vector2(0.3f, 0.04f))), Palette.Hex("CFD8DC"));
                    c.Fill(Sdf.Intersect(can, Sdf.Box(new Vector2(0.5f, 0.1f), new Vector2(0.3f, 0.03f))), Palette.Hex("B0BEC5"));
                    c.Fill(Sdf.Polygon(new Vector2(0.56f, 0.72f), new Vector2(0.4f, 0.47f), new Vector2(0.5f, 0.47f),
                        new Vector2(0.44f, 0.24f), new Vector2(0.62f, 0.52f), new Vector2(0.52f, 0.52f)), Palette.Hex("C6FF00"));
                    break;
                }
                case "food_sandwich":
                {
                    Vector2 a = new Vector2(0.1f, 0.2f), b = new Vector2(0.9f, 0.2f), t = new Vector2(0.5f, 0.85f);
                    c.Fill(Sdf.Grow(Sdf.Triangle(a, b, t), 0.02f), Palette.Hex("C8913F"));
                    c.Fill(Sdf.Triangle(a + new Vector2(0.05f, 0.03f), b + new Vector2(-0.05f, 0.03f), t + new Vector2(0, -0.06f)), Palette.Hex("FFF3D6"));
                    c.Fill(Sdf.Intersect(Sdf.Triangle(a, b, t), Sdf.Box(new Vector2(0.5f, 0.44f), new Vector2(0.5f, 0.045f))), Palette.Hex("66BB6A"));
                    c.Fill(Sdf.Intersect(Sdf.Triangle(a, b, t), Sdf.Box(new Vector2(0.5f, 0.35f), new Vector2(0.5f, 0.04f))), Palette.Hex("E53935"));
                    c.Fill(Sdf.Intersect(Sdf.Triangle(a, b, t), Sdf.Box(new Vector2(0.5f, 0.53f), new Vector2(0.5f, 0.035f))), Palette.Hex("F8BBD0"));
                    c.Fill(Sdf.Box(new Vector2(0.5f, 0.88f), new Vector2(0.012f, 0.1f)), Palette.Hex("D7CCC8"));
                    c.Fill(Sdf.Circle(new Vector2(0.5f, 0.96f), 0.035f), Palette.Hex("558B2F"));
                    break;
                }
                case "food_burger":
                    c.Fill(Sdf.Box(new Vector2(0.5f, 0.2f), new Vector2(0.38f, 0.07f), 0.06f), Palette.Hex("D9963F"));
                    c.Fill(Sdf.Box(new Vector2(0.5f, 0.33f), new Vector2(0.41f, 0.07f), 0.06f), Palette.Hex("5D3A1F"));
                    c.Fill(Sdf.Polygon(new Vector2(0.1f, 0.42f), new Vector2(0.9f, 0.42f), new Vector2(0.78f, 0.34f), new Vector2(0.66f, 0.42f),
                        new Vector2(0.5f, 0.32f), new Vector2(0.36f, 0.42f), new Vector2(0.22f, 0.34f)), Palette.Hex("FFC107"));
                    c.Fill(Sdf.Box(new Vector2(0.5f, 0.44f), new Vector2(0.42f, 0.03f), 0.03f), Palette.Hex("7CB342"));
                    c.Fill(Sdf.Box(new Vector2(0.5f, 0.485f), new Vector2(0.38f, 0.025f), 0.02f), Palette.Hex("E53935"));
                    c.Fill(Sdf.Intersect(Sdf.Ellipse(new Vector2(0.5f, 0.5f), new Vector2(0.4f, 0.34f)), Sdf.HalfPlaneAbove(0.515f)), Palette.Hex("E8A64C"));
                    c.Paint(Sdf.Intersect(Sdf.Ellipse(new Vector2(0.5f, 0.5f), new Vector2(0.4f, 0.34f)), Sdf.HalfPlaneAbove(0.515f)),
                        p => new Color(1, 1, 1, Mathf.Clamp01((p.y - 0.6f) * 0.9f)));
                    foreach (var s in new[] { new Vector2(0.36f, 0.7f), new Vector2(0.5f, 0.76f), new Vector2(0.62f, 0.7f), new Vector2(0.44f, 0.62f), new Vector2(0.58f, 0.6f), new Vector2(0.7f, 0.62f), new Vector2(0.3f, 0.6f) })
                        c.Fill(Sdf.Rotate(Sdf.Ellipse(s, new Vector2(0.022f, 0.012f)), s, s.x * 90f), Palette.Hex("FFF8E1"));
                    break;
                case "food_steak":
                    c.Fill(Sdf.Ellipse(new Vector2(0.5f, 0.42f), new Vector2(0.47f, 0.34f)), Palette.Hex("ECEFF1"));
                    c.Fill(Sdf.Outline(Sdf.Ellipse(new Vector2(0.5f, 0.42f), new Vector2(0.37f, 0.26f)), 0.008f), Palette.Hex("CFD8DC"));
                    c.Fill(Sdf.Rotate(Sdf.Ellipse(new Vector2(0.44f, 0.44f), new Vector2(0.24f, 0.15f)), new Vector2(0.44f, 0.44f), -12f), Palette.Hex("6D3B1E"));
                    c.Fill(Sdf.Rotate(Sdf.Ellipse(new Vector2(0.44f, 0.45f), new Vector2(0.2f, 0.115f)), new Vector2(0.44f, 0.45f), -12f), Palette.Hex("8D4A26"));
                    for (int i = 0; i < 3; i++)
                        c.Fill(Sdf.Rotate(Sdf.Box(new Vector2(0.36f + i * 0.08f, 0.45f), new Vector2(0.012f, 0.11f), 0.01f), new Vector2(0.36f + i * 0.08f, 0.45f), 35f), Palette.Hex("4E2A14"));
                    c.Fill(Sdf.Union(Sdf.Circle(new Vector2(0.73f, 0.5f), 0.065f), Sdf.Circle(new Vector2(0.79f, 0.44f), 0.06f), Sdf.Circle(new Vector2(0.69f, 0.43f), 0.055f)), Palette.Hex("43A047"));
                    c.Fill(Sdf.Ellipse(new Vector2(0.7f, 0.3f), new Vector2(0.09f, 0.06f)), Palette.Hex("FFD54F"));
                    c.Fill(Sdf.Ellipse(new Vector2(0.6f, 0.26f), new Vector2(0.07f, 0.05f)), Palette.Hex("FFCA28"));
                    break;
            }
            return c;
        }

        /// <summary>GLSL-style smoothstep (Unity's Mathf.SmoothStep interpolates between its first two arguments instead).</summary>
        public static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        static float Hash(float x, float y)
        {
            float h = Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }
    }
}
