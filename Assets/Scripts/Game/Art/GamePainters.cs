using Casino.Core.Roulette;
using Casino.Core.Slots;
using UnityEngine;

namespace Casino.Art
{
    /// <summary>Art for the slot machine symbols and the roulette wheel, painted with the SDF rasteriser.</summary>
    public static class GamePainters
    {
        static Vector2 V(float x, float y) => new Vector2(x, y);

        static void Highlight(SdfCanvas c, Vector2 at, Vector2 size, float alpha = 0.55f, float rotate = -30f) =>
            c.Fill(Sdf.Rotate(Sdf.Ellipse(at, size), at, rotate), new Color(1, 1, 1, alpha), 3f);

        /// <summary>Glossy fill: lighter towards the top-left, darker towards the bottom-right.</summary>
        static void Glossy(SdfCanvas c, SdfShape shape, Color color, Vector2 light, float radius)
        {
            Color hi = Palette.Shade(color, 1.35f), lo = Palette.Shade(color, 0.6f);
            c.Paint(shape, p =>
            {
                float d = Mathf.Clamp01((p - light).magnitude / radius);
                return Palette.Mix(hi, lo, d * d);
            });
        }

        // ------------------------------------------------------------------ letters for BAR / WILD

        static SdfShape Letter(char ch, float x, float y, float h, float t)
        {
            float w = h * 0.36f, top = y + h * 0.5f, bot = y - h * 0.5f, mid = y;
            switch (ch)
            {
                case 'B':
                    return Sdf.Union(
                        Sdf.Segment(V(x - w, bot), V(x - w, top), t),
                        Sdf.Segment(V(x - w, top), V(x + w * 0.2f, top), t), Sdf.Segment(V(x - w, mid), V(x + w * 0.3f, mid), t),
                        Sdf.Segment(V(x - w, bot), V(x + w * 0.3f, bot), t),
                        Sdf.Arc(V(x + w * 0.2f, (top + mid) * 0.5f), (top - mid) * 0.5f, t, 0f, 90f),
                        Sdf.Arc(V(x + w * 0.3f, (mid + bot) * 0.5f), (mid - bot) * 0.5f, t, 0f, 90f));
                case 'A':
                    return Sdf.Union(Sdf.Segment(V(x - w, bot), V(x, top), t), Sdf.Segment(V(x, top), V(x + w, bot), t),
                        Sdf.Segment(V(x - w * 0.5f, y - h * 0.12f), V(x + w * 0.5f, y - h * 0.12f), t));
                case 'R':
                    return Sdf.Union(
                        Sdf.Segment(V(x - w, bot), V(x - w, top), t),
                        Sdf.Segment(V(x - w, top), V(x + w * 0.2f, top), t), Sdf.Segment(V(x - w, mid), V(x + w * 0.2f, mid), t),
                        Sdf.Arc(V(x + w * 0.2f, (top + mid) * 0.5f), (top - mid) * 0.5f, t, 0f, 90f),
                        Sdf.Segment(V(x, mid), V(x + w, bot), t));
                case 'W':
                    return Sdf.Union(Sdf.Segment(V(x - w * 1.2f, top), V(x - w * 0.6f, bot), t), Sdf.Segment(V(x - w * 0.6f, bot), V(x, mid + h * 0.1f), t),
                        Sdf.Segment(V(x, mid + h * 0.1f), V(x + w * 0.6f, bot), t), Sdf.Segment(V(x + w * 0.6f, bot), V(x + w * 1.2f, top), t));
                case 'I':
                    return Sdf.Segment(V(x, bot), V(x, top), t);
                case 'L':
                    return Sdf.Union(Sdf.Segment(V(x - w * 0.6f, bot), V(x - w * 0.6f, top), t), Sdf.Segment(V(x - w * 0.6f, bot), V(x + w * 0.7f, bot), t));
                case 'D':
                    return Sdf.Union(Sdf.Segment(V(x - w * 0.8f, bot), V(x - w * 0.8f, top), t),
                        Sdf.Segment(V(x - w * 0.8f, top), V(x - w * 0.1f, top), t), Sdf.Segment(V(x - w * 0.8f, bot), V(x - w * 0.1f, bot), t),
                        Sdf.Arc(V(x - w * 0.1f, y), h * 0.5f, t, 0f, 90f));
                default:
                    return p => 1f;
            }
        }

        static SdfShape Word(string text, Vector2 center, float h, float t, float spacing)
        {
            SdfShape shape = null;
            float x0 = center.x - spacing * (text.Length - 1) * 0.5f;
            for (int i = 0; i < text.Length; i++)
            {
                var l = Letter(text[i], x0 + i * spacing, center.y, h, t);
                shape = shape == null ? l : Sdf.Union(shape, l);
            }
            return shape;
        }

        // ------------------------------------------------------------------ slot symbols

        public static SdfCanvas SlotSymbolImage(SlotSymbol symbol, int size = 160)
        {
            var c = new SdfCanvas(size, size);
            Color gold = Palette.Gold, goldDark = Palette.GoldDark;
            switch (symbol)
            {
                case SlotSymbol.Cherry:
                {
                    Color stem = Palette.Hex("2E8B3A");
                    c.Fill(Sdf.Taper(V(0.38f, 0.48f), V(0.58f, 0.86f), 0.02f, 0.013f), stem);
                    c.Fill(Sdf.Taper(V(0.68f, 0.43f), V(0.58f, 0.86f), 0.02f, 0.013f), stem);
                    var leaf = Sdf.Rotate(Sdf.Ellipse(V(0.72f, 0.84f), V(0.13f, 0.055f)), V(0.72f, 0.84f), 22f);
                    Glossy(c, leaf, Palette.Hex("4CC25A"), V(0.66f, 0.88f), 0.18f);
                    foreach (var (at, r) in new[] { (V(0.36f, 0.34f), 0.17f), (V(0.67f, 0.3f), 0.16f) })
                    {
                        var cherry = Sdf.Circle(at, r);
                        c.Fill(Sdf.Grow(cherry, 0.012f), Palette.Hex("6E0A1E"));
                        Glossy(c, cherry, Palette.Hex("E0163E"), at + V(-0.06f, 0.06f), r * 1.6f);
                        Highlight(c, at + V(-0.06f, 0.07f), V(0.045f, 0.025f));
                    }
                    break;
                }
                case SlotSymbol.Lemon:
                {
                    var at = V(0.5f, 0.5f);
                    var lemon = Sdf.Rotate(Sdf.SmoothUnion(Sdf.Ellipse(at, V(0.33f, 0.24f)),
                        Sdf.Union(Sdf.Circle(V(0.15f, 0.5f), 0.05f), Sdf.Circle(V(0.85f, 0.5f), 0.05f)), 0.08f), at, 20f);
                    c.Fill(Sdf.Grow(lemon, 0.012f), Palette.Hex("A8860A"));
                    Glossy(c, lemon, Palette.Hex("FFDE3B"), V(0.4f, 0.62f), 0.45f);
                    Highlight(c, V(0.4f, 0.6f), V(0.1f, 0.04f), 0.5f, 20f);
                    break;
                }
                case SlotSymbol.Orange:
                {
                    var at = V(0.5f, 0.46f);
                    var orange = Sdf.Circle(at, 0.33f);
                    c.Fill(Sdf.Grow(orange, 0.012f), Palette.Hex("9A4A06"));
                    Glossy(c, orange, Palette.Hex("FF8A1F"), V(0.4f, 0.58f), 0.5f);
                    for (int i = 0; i < 14; i++)
                    {
                        float a = i * 2.4f, r = 0.08f + (i % 5) * 0.045f;
                        c.Fill(Sdf.Circle(at + V(Mathf.Cos(a) * r, Mathf.Sin(a) * r), 0.008f), new Color(0.6f, 0.25f, 0f, 0.25f), 2f);
                    }
                    var leaf = Sdf.Rotate(Sdf.Ellipse(V(0.6f, 0.82f), V(0.12f, 0.05f)), V(0.6f, 0.82f), -20f);
                    Glossy(c, leaf, Palette.Hex("4CC25A"), V(0.55f, 0.85f), 0.15f);
                    Highlight(c, V(0.38f, 0.6f), V(0.07f, 0.04f));
                    break;
                }
                case SlotSymbol.Plum:
                {
                    var at = V(0.5f, 0.44f);
                    var plum = Sdf.Ellipse(at, V(0.29f, 0.33f));
                    c.Fill(Sdf.Grow(plum, 0.012f), Palette.Hex("3A0E5C"));
                    Glossy(c, plum, Palette.Hex("8E3BD6"), V(0.4f, 0.56f), 0.48f);
                    c.Fill(Sdf.Arc(V(0.62f, 0.44f), 0.16f, 0.008f, 180f, 55f), new Color(0.2f, 0f, 0.3f, 0.4f), 3f);
                    c.Fill(Sdf.Taper(V(0.5f, 0.74f), V(0.56f, 0.88f), 0.018f, 0.012f), Palette.Hex("5A3A1A"));
                    Highlight(c, V(0.4f, 0.58f), V(0.06f, 0.035f));
                    break;
                }
                case SlotSymbol.Bell:
                {
                    var bell = Sdf.SmoothUnion(Sdf.Ellipse(V(0.5f, 0.6f), V(0.22f, 0.24f)),
                        Sdf.Polygon(V(0.2f, 0.3f), V(0.8f, 0.3f), V(0.68f, 0.6f), V(0.32f, 0.6f)), 0.08f);
                    c.Fill(Sdf.Circle(V(0.5f, 0.21f), 0.07f), goldDark);
                    c.Fill(Sdf.Grow(bell, 0.012f), Palette.Hex("7A5410"));
                    Glossy(c, bell, gold, V(0.4f, 0.66f), 0.5f);
                    c.Fill(Sdf.Box(V(0.5f, 0.3f), V(0.32f, 0.035f), 0.03f), goldDark);
                    c.Fill(Sdf.Circle(V(0.5f, 0.86f), 0.045f), goldDark);
                    Highlight(c, V(0.4f, 0.64f), V(0.05f, 0.11f), 0.5f, -10f);
                    break;
                }
                case SlotSymbol.Bar:
                {
                    var plate = Sdf.Box(V(0.5f, 0.5f), V(0.44f, 0.2f), 0.06f);
                    c.Fill(Sdf.Grow(plate, 0.02f), gold);
                    c.Fill(plate, Palette.Hex("17141C"));
                    var word = Word("BAR", V(0.5f, 0.5f), 0.2f, 0.024f, 0.25f);
                    c.Fill(Sdf.Grow(word, 0.012f), Palette.Hex("6B2A00"));
                    Glossy(c, word, gold, V(0.4f, 0.6f), 0.4f);
                    break;
                }
                case SlotSymbol.Seven:
                {
                    var seven = Sdf.Union(Sdf.Segment(V(0.28f, 0.76f), V(0.72f, 0.76f), 0.07f), Sdf.Taper(V(0.7f, 0.74f), V(0.44f, 0.18f), 0.075f, 0.07f));
                    c.Fill(Sdf.Grow(seven, 0.035f), gold);
                    c.Fill(Sdf.Grow(seven, 0.012f), Palette.Hex("6E0A1E"));
                    Glossy(c, seven, Palette.Hex("E81C3E"), V(0.4f, 0.75f), 0.6f);
                    Highlight(c, V(0.4f, 0.78f), V(0.08f, 0.02f), 0.45f, 0f);
                    break;
                }
                case SlotSymbol.Diamond:
                {
                    var gem = Sdf.Polygon(V(0.34f, 0.74f), V(0.66f, 0.74f), V(0.84f, 0.58f), V(0.5f, 0.16f), V(0.16f, 0.58f));
                    c.Fill(Sdf.Grow(gem, 0.014f), Palette.Hex("0E5A7A"));
                    c.Fill(gem, Palette.Hex("35C8F0"));
                    c.Fill(Sdf.Triangle(V(0.34f, 0.74f), V(0.16f, 0.58f), V(0.5f, 0.58f)), Palette.Hex("9CEBFF"));
                    c.Fill(Sdf.Triangle(V(0.66f, 0.74f), V(0.84f, 0.58f), V(0.5f, 0.58f)), Palette.Hex("1FA6D6"));
                    c.Fill(Sdf.Triangle(V(0.16f, 0.58f), V(0.5f, 0.58f), V(0.5f, 0.16f)), Palette.Hex("62D9FF"));
                    c.Fill(Sdf.Triangle(V(0.84f, 0.58f), V(0.5f, 0.58f), V(0.5f, 0.16f)), Palette.Hex("1B8FC0"));
                    c.Fill(Sdf.Star5(V(0.32f, 0.68f), 0.07f, 0.35f), new Color(1, 1, 1, 0.9f));
                    break;
                }
                case SlotSymbol.Wild:
                {
                    var card = Sdf.Box(V(0.5f, 0.54f), V(0.29f, 0.38f), 0.05f);
                    c.Fill(Sdf.Grow(card, 0.02f), gold);
                    c.Fill(card, Palette.Hex("FFF9EE"));
                    var spade = Sdf.Union(Sdf.Heart(V(0.5f, 0.78f), 0.26f, true),
                        Sdf.Triangle(V(0.5f, 0.6f), V(0.43f, 0.46f), V(0.57f, 0.46f)));
                    c.Fill(spade, Palette.Hex("17131F"));
                    c.Fill(Letter('A', 0.32f, 0.82f, 0.09f, 0.012f), Palette.Hex("17131F"));
                    var ribbon = Sdf.Box(V(0.5f, 0.24f), V(0.42f, 0.085f), 0.02f);
                    c.Fill(Sdf.Grow(ribbon, 0.012f), Palette.Hex("6B0A1E"));
                    Glossy(c, ribbon, Palette.Hex("E0163E"), V(0.4f, 0.3f), 0.5f);
                    c.Fill(Word("WILD", V(0.5f, 0.24f), 0.1f, 0.014f, 0.17f), gold);
                    break;
                }
                default: // Scatter star
                {
                    c.Paint(Sdf.Circle(V(0.5f, 0.5f), 0.5f), p => new Color(1f, 0.85f, 0.3f, Mathf.Pow(Mathf.Clamp01(1f - (p - V(0.5f, 0.5f)).magnitude * 2f), 2f) * 0.6f));
                    var star = Sdf.Star5(V(0.5f, 0.5f), 0.4f, 0.47f);
                    c.Fill(Sdf.Grow(star, 0.016f), Palette.Hex("8A4A00"));
                    Glossy(c, star, Palette.Hex("FFC83A"), V(0.42f, 0.62f), 0.5f);
                    c.Fill(Sdf.Star5(V(0.5f, 0.5f), 0.2f, 0.47f), new Color(1f, 0.97f, 0.8f, 0.6f), 3f);
                    break;
                }
            }
            return c;
        }

        // ------------------------------------------------------------------ roulette

        /// <summary>Pocket i (in wheel order) is centred this many degrees clockwise from the top.</summary>
        public static float PocketAngle(int index) => (index + 0.5f) * 360f / RouletteRules.Pockets;

        /// <summary>The wheel, top view. Numbers are added as text by the screen.</summary>
        public static SdfCanvas RouletteWheel(int size = 512)
        {
            var c = new SdfCanvas(size, size);
            var center = V(0.5f, 0.5f);
            Color wood = Palette.Hex("5A2410"), woodLight = Palette.Hex("8A3E1C"), gold = Palette.Gold;
            c.Paint(Sdf.Circle(center, 0.5f), p =>
            {
                float d = (p - center).magnitude;
                return Palette.Mix(woodLight, wood, Mathf.Abs(Mathf.Sin(d * 60f)) * 0.4f + (d - 0.4f) * 2f);
            });
            c.Fill(Sdf.Ring(center, 0.468f, 0.006f), gold);
            // ball track
            c.Paint(Sdf.Ring(center, 0.445f, 0.018f), p => Palette.Mix(Palette.Hex("3A1A10"), Palette.Hex("6B3A22"), Mathf.Clamp01(((p - center).magnitude - 0.427f) * 25f)));
            // pockets
            Color red = Palette.Hex("C0122F"), black = Palette.Hex("17141C"), green = Palette.Hex("0D7A45");
            float step = 360f / RouletteRules.Pockets;
            c.Paint(Sdf.Ring(center, 0.36f, 0.064f), p =>
            {
                Vector2 d = p - center;
                float ang = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
                if (ang < 0f) ang += 360f;
                int i = Mathf.FloorToInt(ang / step) % RouletteRules.Pockets;
                int n = RouletteRules.WheelOrder[i];
                float edge = Mathf.Min(ang - i * step, (i + 1) * step - ang);
                if (edge < 0.45f) return gold;
                Color col = n == 0 ? green : RouletteRules.IsRed(n) ? red : black;
                float r = d.magnitude;
                return r < 0.33f ? Palette.Shade(col, 0.7f) : col;
            });
            c.Fill(Sdf.Ring(center, 0.297f, 0.005f), gold);
            c.Fill(Sdf.Ring(center, 0.424f, 0.004f), gold);
            // cone and turret
            c.Paint(Sdf.Circle(center, 0.29f), p =>
            {
                float d = (p - center).magnitude / 0.29f;
                return Palette.Mix(Palette.Hex("9A4A22"), Palette.Hex("4A1A0C"), d * d);
            });
            for (int k = 0; k < 4; k++)
            {
                float a = k * Mathf.PI * 0.5f;
                var dir = V(Mathf.Sin(a), Mathf.Cos(a));
                c.Fill(Sdf.Taper(center, center + dir * 0.2f, 0.022f, 0.01f), gold);
                c.Fill(Sdf.Circle(center + dir * 0.2f, 0.022f), Palette.GoldLight);
            }
            c.Fill(Sdf.Circle(center, 0.06f), gold);
            c.Fill(Sdf.Circle(center + V(-0.015f, 0.015f), 0.025f), Palette.GoldLight);
            return c;
        }

        public static SdfCanvas Ball(int size = 48)
        {
            var c = new SdfCanvas(size, size);
            c.Fill(Sdf.Circle(V(0.52f, 0.46f), 0.42f), new Color(0, 0, 0, 0.35f), 4f);
            Glossy(c, Sdf.Circle(V(0.5f, 0.5f), 0.4f), new Color(0.92f, 0.92f, 0.95f), V(0.38f, 0.62f), 0.6f);
            Highlight(c, V(0.38f, 0.62f), V(0.1f, 0.07f), 0.8f);
            return c;
        }
    }
}
