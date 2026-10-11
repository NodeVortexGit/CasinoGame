using Casino.Core.Player;
using UnityEngine;

namespace Casino.Art
{
    public enum Expression { Smile, Talk, Wink, Blink }

    public struct AvatarLook
    {
        public bool Female;
        public int HairStyle;
        public StaffRole Role;
        public Color Skin, Hair, Eyes, Lips, Outfit, Accent, Background;
    }

    /// <summary>
    /// Flat-vector portrait painter for the casino staff (host, dealers, cashier, bar). Everything is a stack of SDF
    /// layers, so the same look renders identically in each expression. Drop real art into Resources/Dealers to replace it.
    /// </summary>
    public static partial class AvatarPainter
    {
        static readonly Color[] Skins = { Palette.Hex("FBD9C4"), Palette.Hex("F2C3A0"), Palette.Hex("DDA47A"), Palette.Hex("C68A5E"), Palette.Hex("9C6644"), Palette.Hex("6E4630") };
        static readonly Color[] Hairs = { Palette.Hex("1C1414"), Palette.Hex("3B2416"), Palette.Hex("6B3A1F"), Palette.Hex("8E3B1E"), Palette.Hex("D9B15F"), Palette.Hex("EBD9B0"), Palette.Hex("A52A2A") };
        static readonly Color[] EyeColors = { Palette.Hex("5B3A1E"), Palette.Hex("7A5A2B"), Palette.Hex("3F7D4E"), Palette.Hex("3A78C2"), Palette.Hex("6B7B8C") };
        static readonly Color[] LipColors = { Palette.Hex("D2455F"), Palette.Hex("B8203A"), Palette.Hex("C85A7C"), Palette.Hex("D8797A") };

        public static AvatarLook LookFor(bool female, int seed, StaffRole role)
        {
            uint h = (uint)(seed * 2654435761u) ^ (female ? 0x9E3779B9u : 0x7F4A7C15u);
            int Pick(int n) { h = h * 1664525u + 1013904223u; return (int)((h >> 8) % (uint)n); }

            var look = new AvatarLook
            {
                Female = female,
                Role = role,
                HairStyle = Pick(3),
                Skin = Skins[Pick(Skins.Length)],
                Hair = Hairs[Pick(Hairs.Length)],
                Eyes = EyeColors[Pick(EyeColors.Length)],
                Lips = LipColors[Pick(LipColors.Length)],
            };
            switch (role)
            {
                case StaffRole.Host:
                    look.Outfit = female ? new[] { Palette.Hex("8E1538"), Palette.Hex("0F6B4F"), Palette.Hex("2A3FA8") }[Pick(3)] : Palette.Hex("15131A");
                    look.Accent = Palette.Gold;
                    look.Background = Palette.Hex("5B2A9E");
                    break;
                case StaffRole.Cashier:
                    look.Outfit = Palette.Hex("1B2A4E");
                    look.Accent = Palette.Gold;
                    look.Background = Palette.Hex("1F6F8B");
                    break;
                case StaffRole.Server:
                    look.Outfit = female ? Palette.Hex("C0122F") : Palette.Hex("6E1830");
                    look.Accent = Palette.Hex("15131A");
                    look.Background = Palette.Hex("B4532A");
                    break;
                default:
                    look.Outfit = Palette.Hex("17151C");
                    look.Accent = new[] { Palette.Hex("C8102E"), Palette.Gold, Palette.Hex("7B2CBF") }[Pick(3)];
                    look.Background = Palette.Hex("0E6B44");
                    break;
            }
            return look;
        }

        public static SdfCanvas Paint(AvatarLook look, Expression expression, int size = 256)
        {
            var c = new SdfCanvas(size, size);
            Background(c, look);
            BackHair(c, look);
            Body(c, look);
            Head(c, look);
            FrontHair(c, look);
            Face(c, look, expression);
            if (look.Female && look.HairStyle != 0) Earrings(c, look);
            c.Mask(Sdf.Circle(new Vector2(0.5f, 0.5f), 0.495f));
            return c;
        }

        static Vector2 V(float x, float y) => new Vector2(x, y);

        static void Background(SdfCanvas c, AvatarLook look)
        {
            Color inner = Palette.Shade(look.Background, 1.35f), outer = Palette.Shade(look.Background, 0.55f);
            c.Paint(_ => -1f, p =>
            {
                float d = Mathf.Clamp01((p - V(0.5f, 0.62f)).magnitude * 1.6f);
                return Palette.Mix(inner, outer, d);
            });
        }

        // Adult proportions: an oval with a tapered, delicate chin for women; a squarer jaw and chin for men.
        static SdfShape HeadShape(bool female) => female
            ? Sdf.SmoothUnion(
                Sdf.Ellipse(V(0.5f, 0.572f), V(0.147f, 0.172f)),
                Sdf.Grow(Sdf.Triangle(V(0.385f, 0.53f), V(0.615f, 0.53f), V(0.5f, 0.398f)), 0.036f), 0.07f)
            : Sdf.SmoothUnion(
                Sdf.Ellipse(V(0.5f, 0.578f), V(0.152f, 0.17f)),
                Sdf.Box(V(0.5f, 0.452f), V(0.118f, 0.07f), 0.058f), 0.06f);

        static SdfShape BodyShape(bool female) => Sdf.SmoothUnion(
            Sdf.Ellipse(V(0.5f, 0.05f), female ? V(0.38f, 0.225f) : V(0.43f, 0.23f)),
            Sdf.Box(V(0.5f, -0.06f), V(female ? 0.38f : 0.43f, 0.12f), 0.04f), 0.06f);

        static void BackHair(SdfCanvas c, AvatarLook look)
        {
            Color back = Palette.Shade(look.Hair, 0.8f);
            if (look.Female)
            {
                switch (look.HairStyle)
                {
                    case 0: // long waves
                        c.Fill(Sdf.SmoothUnion(Sdf.Ellipse(V(0.5f, 0.6f), V(0.2f, 0.215f)),
                            Sdf.Ellipse(V(0.5f, 0.42f), V(0.215f, 0.25f)), 0.08f), back);
                        break;
                    case 1: // bun
                        c.Fill(Sdf.Circle(V(0.5f, 0.8f), 0.085f), back);
                        c.Fill(Sdf.Arc(V(0.5f, 0.8f), 0.05f, 0.01f, 130f, 40f), Palette.Shade(look.Hair, 1.25f));
                        break;
                    default: // bob
                        c.Fill(Sdf.SmoothUnion(Sdf.Ellipse(V(0.5f, 0.57f), V(0.2f, 0.215f)),
                            Sdf.Box(V(0.5f, 0.44f), V(0.198f, 0.085f), 0.06f), 0.04f), back);
                        break;
                }
            }
        }

        static void Body(SdfCanvas c, AvatarLook look)
        {
            if (look.Female)
            {
                FemaleBust(c, look);
                return;
            }
            Color skinShadow = Palette.Shade(look.Skin, 0.86f);
            c.Fill(Sdf.Box(V(0.5f, 0.31f), V(look.Female ? 0.046f : 0.055f, 0.09f), 0.03f), Palette.Shade(look.Skin, 0.94f));
            c.Fill(Sdf.Ellipse(V(0.5f, 0.375f), V(0.07f, 0.035f)), Palette.WithAlpha(skinShadow, 0.8f), 6f);

            var body = BodyShape(look.Female);
            Color white = Palette.Hex("F7F5F2");
            switch (look.Role)
            {
                case StaffRole.Cashier:
                {
                    c.Fill(body, look.Outfit);
                    var shirt = Sdf.Triangle(V(0.42f, 0.28f), V(0.58f, 0.28f), V(0.5f, 0.1f));
                    c.Fill(Sdf.Intersect(body, shirt), white);
                    c.Fill(Sdf.Intersect(body, Sdf.Triangle(V(0.42f, 0.28f), V(0.47f, 0.17f), V(0.35f, 0.2f))), Palette.Shade(look.Outfit, 0.7f));
                    c.Fill(Sdf.Intersect(body, Sdf.Triangle(V(0.58f, 0.28f), V(0.53f, 0.17f), V(0.65f, 0.2f))), Palette.Shade(look.Outfit, 0.7f));
                    c.Fill(Sdf.Polygon(V(0.49f, 0.25f), V(0.51f, 0.25f), V(0.52f, 0.13f), V(0.5f, 0.1f), V(0.48f, 0.13f)), Palette.Hex("8E1538"));
                    c.Fill(Sdf.Box(V(0.33f, 0.13f), V(0.045f, 0.016f), 0.006f), look.Accent);
                    Sheen(c, body);
                    return;
                }
                default:
                {
                    // Dealer, server and host: shirt with a vest / jacket and a bow tie.
                    c.Fill(body, white);
                    Color vest = look.Outfit;
                    c.Fill(Sdf.Intersect(body, Sdf.Polygon(V(0.0f, -0.2f), V(0.0f, 0.3f), V(0.38f, 0.27f), V(0.475f, 0.06f), V(0.475f, -0.2f))), vest);
                    c.Fill(Sdf.Intersect(body, Sdf.Polygon(V(1.0f, -0.2f), V(1.0f, 0.3f), V(0.62f, 0.27f), V(0.525f, 0.06f), V(0.525f, -0.2f))), vest);
                    if (look.Role == StaffRole.Host)
                    {
                        c.Fill(Sdf.Intersect(body, Sdf.Triangle(V(0.38f, 0.27f), V(0.47f, 0.08f), V(0.33f, 0.16f))), Palette.Shade(vest, 1.6f));
                        c.Fill(Sdf.Intersect(body, Sdf.Triangle(V(0.62f, 0.27f), V(0.53f, 0.08f), V(0.67f, 0.16f))), Palette.Shade(vest, 1.6f));
                    }
                    c.Fill(Sdf.Triangle(V(0.44f, 0.275f), V(0.5f, 0.23f), V(0.45f, 0.2f)), Palette.Hex("E6E2DC"));
                    c.Fill(Sdf.Triangle(V(0.56f, 0.275f), V(0.5f, 0.23f), V(0.55f, 0.2f)), Palette.Hex("E6E2DC"));
                    Color tie = look.Accent;
                    c.Fill(Sdf.Triangle(V(0.5f, 0.228f), V(0.448f, 0.255f), V(0.448f, 0.2f)), tie);
                    c.Fill(Sdf.Triangle(V(0.5f, 0.228f), V(0.552f, 0.255f), V(0.552f, 0.2f)), tie);
                    c.Fill(Sdf.Circle(V(0.5f, 0.228f), 0.014f), Palette.Shade(tie, 0.8f));
                    foreach (float y in new[] { 0.06f, 0.0f })
                        c.Fill(Sdf.Circle(V(0.5f, y), 0.008f), Palette.Shade(vest, 1.8f));
                    if (look.Role == StaffRole.Dealer)
                        c.Fill(Sdf.Box(V(0.67f, 0.13f), V(0.045f, 0.016f), 0.006f), Palette.Gold);
                    Sheen(c, body);
                    return;
                }
            }
        }

        /// <summary>Bare shoulders and a sweetheart-neckline cocktail dress in the role's colour.</summary>
        static void FemaleBust(SdfCanvas c, AvatarLook look)
        {
            Color skinShade = Palette.Shade(look.Skin, 0.8f);
            c.Fill(Sdf.Taper(V(0.5f, 0.4f), V(0.5f, 0.26f), 0.043f, 0.05f), Palette.Shade(look.Skin, 0.95f));
            c.Fill(Sdf.Ellipse(V(0.5f, 0.372f), V(0.07f, 0.034f)), Palette.WithAlpha(skinShade, 0.7f), 6f);
            var body = BodyShape(true);
            c.Fill(body, look.Skin);
            c.Paint(body, p => new Color(skinShade.r, skinShade.g, skinShade.b, Mathf.Clamp01(Mathf.Abs(p.x - 0.5f) * 1.3f - 0.12f) * 0.5f));
            for (int s = -1; s <= 1; s += 2)
                c.Fill(Sdf.Taper(V(0.5f + s * 0.03f, 0.25f), V(0.5f + s * 0.12f, 0.262f), 0.0045f, 0.002f), Palette.WithAlpha(skinShade, 0.45f), 3f);

            Color dress = DressColor(look);
            var top = Sdf.Union(Sdf.Circle(V(0.445f, 0.15f), 0.088f), Sdf.Circle(V(0.555f, 0.15f), 0.088f), Sdf.HalfPlaneBelow(0.15f));
            var shape = Sdf.Intersect(Sdf.Grow(body, 0.004f), top);
            for (int s = -1; s <= 1; s += 2)
                c.Fill(Sdf.Taper(V(0.5f + s * 0.07f, 0.22f), V(0.5f + s * 0.115f, 0.262f), 0.0085f, 0.0075f), Palette.Shade(dress, 0.9f));
            c.Paint(shape, p => DressShade(dress, p, 0.5f, 0.3f, look.Role == StaffRole.Host));
            Color trim = look.Role == StaffRole.Server ? Palette.Hex("15131A") : Palette.Gold;
            c.Fill(Sdf.Intersect(Sdf.Grow(body, 0.004f), Sdf.Outline(top, 0.005f)), trim);

            switch (look.Role)
            {
                case StaffRole.Dealer:
                    Bow(c, V(0.5f, 0.33f), 0.026f, look.Accent);
                    c.Fill(Sdf.Box(V(0.64f, 0.09f), V(0.04f, 0.014f), 0.006f), Palette.Gold);
                    break;
                case StaffRole.Cashier:
                    c.Fill(Sdf.Box(V(0.36f, 0.09f), V(0.04f, 0.014f), 0.006f), look.Accent);
                    c.Fill(Sdf.Circle(V(0.5f, 0.12f), 0.01f), Palette.Gold);
                    break;
                case StaffRole.Host:
                    c.Fill(Sdf.Arc(V(0.5f, 0.335f), 0.085f, 0.004f, 270f, 52f), look.Accent);
                    c.Fill(Sdf.Rhombus(V(0.5f, 0.238f), V(0.015f, 0.022f)), Palette.Hex("8DF3FF"));
                    break;
                default:
                    c.Fill(Sdf.Arc(V(0.5f, 0.335f), 0.085f, 0.0035f, 270f, 50f), Palette.Gold);
                    break;
            }
        }

        static Color DressColor(AvatarLook look) => look.Role == StaffRole.Dealer ? Palette.Hex("141218") : look.Outfit;

        /// <summary>Satin shading: darker at the sides, a soft highlight left of centre, optional sequin glitter.</summary>
        static Color DressShade(Color dress, Vector2 p, float centreX, float halfWidth, bool sequins)
        {
            float side = Mathf.Abs(p.x - centreX) / halfWidth;
            float light = 1.12f - side * side * 0.45f + Mathf.Clamp01(0.25f - Mathf.Abs(p.x - (centreX - halfWidth * 0.35f)) / halfWidth) * 0.6f;
            Color col = Palette.Shade(dress, light);
            if (sequins)
            {
                // Sparkling sequins: a few cells of a fine grid get a soft round glint.
                const float cell = 0.0065f;
                int gx = Mathf.FloorToInt(p.x / cell), gy = Mathf.FloorToInt(p.y / cell);
                uint h = (uint)(gx * 73856093) ^ (uint)(gy * 19349663);
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                float r = (h & 1023) / 1023f;
                if (r > 0.72f)
                {
                    float dx = p.x / cell - gx - 0.5f, dy = p.y / cell - gy - 0.5f;
                    float glint = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2.6f);
                    col = Palette.Mix(col, Color.white, glint * (r - 0.72f) * 3f);
                }
            }
            col.a = 1f;
            return col;
        }

        static void Bow(SdfCanvas c, Vector2 at, float size, Color color)
        {
            c.Fill(Sdf.Triangle(at, at + V(-size, size * 0.55f), at + V(-size, -size * 0.55f)), color);
            c.Fill(Sdf.Triangle(at, at + V(size, size * 0.55f), at + V(size, -size * 0.55f)), color);
            c.Fill(Sdf.Circle(at, size * 0.28f), Palette.Shade(color, 0.8f));
        }

        static void Sheen(SdfCanvas c, SdfShape body) =>
            c.Paint(body, p => new Color(1, 1, 1, Mathf.Clamp01(0.07f - Mathf.Abs(p.x - 0.3f) * 0.35f)));

        static void Head(SdfCanvas c, AvatarLook look)
        {
            bool f = look.Female;
            Color ear = Palette.Shade(look.Skin, 0.95f);
            float ex = f ? 0.357f : 0.352f;
            c.Fill(Sdf.Ellipse(V(ex, 0.535f), V(0.024f, 0.04f)), ear);
            c.Fill(Sdf.Ellipse(V(1f - ex, 0.535f), V(0.024f, 0.04f)), ear);
            c.Fill(Sdf.Ellipse(V(ex + 0.003f, 0.535f), V(0.011f, 0.022f)), Palette.Shade(look.Skin, 0.82f), 2f);
            c.Fill(Sdf.Ellipse(V(1f - ex - 0.003f, 0.535f), V(0.011f, 0.022f)), Palette.Shade(look.Skin, 0.82f), 2f);

            var head = HeadShape(f);
            c.Fill(head, look.Skin);
            HeadShading(c, look, head);
        }

        /// <summary>Contour: soft shading on the temples and jaw, under the cheekbones, a highlight on top of them, stubble.</summary>
        static void HeadShading(SdfCanvas c, AvatarLook look, SdfShape head)
        {
            bool f = look.Female;
            Color shade = new Color(look.Skin.r * 0.72f, look.Skin.g * 0.6f, look.Skin.b * 0.58f, 1f);
            c.Paint(head, p =>
            {
                float side = Mathf.Abs(p.x - 0.5f) / 0.15f;
                float k = Mathf.Clamp01(side * side - 0.5f) * 0.24f + Mathf.Clamp01((0.44f - p.y) * 2.4f) * 0.1f;
                return new Color(shade.r, shade.g, shade.b, k);
            });
            Color hollow = Palette.WithAlpha(shade, f ? 0.2f : 0.26f);
            c.Fill(Sdf.Rotate(Sdf.Ellipse(V(0.395f, 0.478f), V(0.04f, 0.012f)), V(0.395f, 0.478f), -32f), hollow, 14f);
            c.Fill(Sdf.Rotate(Sdf.Ellipse(V(0.605f, 0.478f), V(0.04f, 0.012f)), V(0.605f, 0.478f), 32f), hollow, 14f);
            Color glow = new Color(1f, 0.97f, 0.92f, f ? 0.2f : 0.12f);
            c.Fill(Sdf.Ellipse(V(0.41f, 0.515f), V(0.03f, 0.014f)), glow, 10f);
            c.Fill(Sdf.Ellipse(V(0.59f, 0.515f), V(0.03f, 0.014f)), glow, 10f);
            if (!f && look.HairStyle != 2)
            {
                // Light designer stubble along the jaw and upper lip.
                Color stubble = Palette.WithAlpha(Palette.Mix(look.Hair, look.Skin, 0.35f), 0.2f);
                c.Paint(Sdf.Intersect(head, Sdf.HalfPlaneBelow(0.47f)), p =>
                {
                    float mouth = Mathf.Clamp01(1f - ((p - V(0.5f, 0.452f)).magnitude - 0.03f) * 40f);
                    float a = Mathf.Clamp01((0.47f - p.y) * 22f) * (1f - mouth * 0.7f);
                    return new Color(stubble.r, stubble.g, stubble.b, stubble.a * a);
                }, 2f);
            }
        }

        static void FrontHair(SdfCanvas c, AvatarLook look)
        {
            Color hair = look.Hair;
            Color shine = Palette.WithAlpha(Palette.Shade(look.Hair, 1.6f), 0.45f);
            SdfShape cap, part = null;
            if (look.Female)
            {
                switch (look.HairStyle)
                {
                    case 0:
                        cap = Sdf.Union(
                            Sdf.Subtract(Sdf.Ellipse(V(0.5f, 0.605f), V(0.18f, 0.19f)), Sdf.Ellipse(V(0.54f, 0.505f), V(0.16f, 0.168f))),
                            Sdf.Rotate(Sdf.Ellipse(V(0.34f, 0.42f), V(0.038f, 0.2f)), V(0.34f, 0.42f), -6f),
                            Sdf.Rotate(Sdf.Ellipse(V(0.662f, 0.43f), V(0.034f, 0.19f)), V(0.662f, 0.43f), 6f));
                        break;
                    case 1:
                        cap = Sdf.Subtract(Sdf.Ellipse(V(0.5f, 0.605f), V(0.17f, 0.18f)), Sdf.Ellipse(V(0.5f, 0.505f), V(0.152f, 0.152f)));
                        cap = Sdf.Union(cap, Sdf.Ellipse(V(0.352f, 0.52f), V(0.016f, 0.06f)), Sdf.Ellipse(V(0.648f, 0.52f), V(0.016f, 0.06f)));
                        break;
                    default:
                        cap = Sdf.Union(
                            Sdf.Intersect(Sdf.Ellipse(V(0.5f, 0.6f), V(0.182f, 0.192f)), Sdf.HalfPlaneAbove(0.598f)),
                            Sdf.Box(V(0.343f, 0.49f), V(0.04f, 0.12f), 0.03f),
                            Sdf.Box(V(0.657f, 0.49f), V(0.04f, 0.12f), 0.03f));
                        break;
                }
            }
            else
            {
                switch (look.HairStyle)
                {
                    case 0: // short with a quiff
                        cap = Sdf.Union(
                            Sdf.Subtract(Sdf.Ellipse(V(0.5f, 0.615f), V(0.172f, 0.178f)), Sdf.Box(V(0.5f, 0.49f), V(0.153f, 0.152f), 0.075f)),
                            Sdf.Rotate(Sdf.Ellipse(V(0.485f, 0.75f), V(0.14f, 0.058f)), V(0.485f, 0.75f), 8f));
                        break;
                    case 1: // side-swept with a part
                        cap = Sdf.Union(
                            Sdf.Subtract(Sdf.Ellipse(V(0.5f, 0.615f), V(0.172f, 0.178f)), Sdf.Box(V(0.5f, 0.49f), V(0.153f, 0.15f), 0.07f)),
                            Sdf.Rotate(Sdf.Ellipse(V(0.53f, 0.72f), V(0.14f, 0.055f)), V(0.53f, 0.72f), -12f));
                        part = Sdf.Intersect(cap, Sdf.Segment(V(0.405f, 0.66f), V(0.418f, 0.8f), 0.0045f));
                        break;
                    default: // close crop
                        cap = Sdf.Subtract(Sdf.Ellipse(V(0.5f, 0.6f), V(0.164f, 0.176f)), Sdf.Box(V(0.5f, 0.5f), V(0.152f, 0.155f), 0.08f));
                        hair = Palette.Mix(look.Hair, look.Skin, 0.25f);
                        break;
                }
            }
            c.Fill(cap, hair);
            if (part != null) c.Fill(part, Palette.Shade(hair, 0.55f), 1.2f);
            c.Fill(Sdf.Intersect(cap, Sdf.Arc(V(0.5f, 0.6f), 0.14f, 0.012f, 118f, 22f)), shine, 2.5f);
            c.Fill(Sdf.Intersect(cap, Sdf.Arc(V(0.5f, 0.58f), 0.17f, 0.006f, 70f, 16f)), Palette.WithAlpha(Palette.Shade(look.Hair, 0.6f), 0.6f), 1.5f);
        }

        static void Face(SdfCanvas c, AvatarLook look, Expression expression)
        {
            bool f = look.Female;
            Color brow = Palette.Shade(look.Hair, look.Hair.r > 0.7f ? 0.62f : 0.85f);
            Color line = Palette.Hex("1E1216");

            // Brows: a high, tapered arch for women; straight, strong and low for men.
            for (int s = -1; s <= 1; s += 2)
            {
                float ex = 0.5f + s * 0.062f;
                if (f)
                {
                    c.Fill(Sdf.Taper(V(ex - s * 0.03f, 0.592f), V(ex + s * 0.012f, 0.607f), 0.0062f, 0.0046f), brow, 1.1f);
                    c.Fill(Sdf.Taper(V(ex + s * 0.012f, 0.607f), V(ex + s * 0.042f, 0.592f), 0.0046f, 0.0016f), brow, 1.1f);
                }
                else
                {
                    c.Fill(Sdf.Taper(V(ex - s * 0.034f, 0.6f), V(ex + s * 0.006f, 0.607f), 0.0088f, 0.0074f), brow, 1.1f);
                    c.Fill(Sdf.Taper(V(ex + s * 0.006f, 0.607f), V(ex + s * 0.038f, 0.6f), 0.0074f, 0.0035f), brow, 1.1f);
                }
            }

            Eye(c, look, V(0.438f, 0.557f), -1f, expression == Expression.Blink, line);
            Eye(c, look, V(0.562f, 0.557f), 1f, expression == Expression.Wink || expression == Expression.Blink, line);

            // Nose: a soft bridge shadow, a highlight and the tip.
            Color noseShadow = Palette.WithAlpha(Palette.Shade(look.Skin, 0.74f), 0.45f);
            c.Fill(Sdf.Taper(V(0.488f, 0.56f), V(0.491f, f ? 0.505f : 0.5f), 0.0022f, 0.0045f), noseShadow, 2.5f);
            c.Fill(Sdf.Taper(V(0.506f, 0.55f), V(0.505f, 0.515f), 0.002f, 0.004f), new Color(1, 1, 1, 0.16f), 3f);
            float ny = f ? 0.5f : 0.494f;
            c.Fill(Sdf.Arc(V(0.5f, ny + 0.008f), f ? 0.014f : 0.019f, 0.0028f, 270f, 55f), Palette.WithAlpha(Palette.Shade(look.Skin, 0.66f), 0.7f), 1.2f);
            c.Fill(Sdf.Ellipse(V(0.486f, ny + 0.004f), V(0.005f, 0.0028f)), Palette.WithAlpha(Palette.Shade(look.Skin, 0.55f), 0.5f), 1.5f);
            c.Fill(Sdf.Ellipse(V(0.514f, ny + 0.004f), V(0.005f, 0.0028f)), Palette.WithAlpha(Palette.Shade(look.Skin, 0.55f), 0.5f), 1.5f);

            if (f)
            {
                Color blush = new Color(0.95f, 0.4f, 0.46f, 0.24f);
                c.Fill(Sdf.Rotate(Sdf.Ellipse(V(0.41f, 0.5f), V(0.034f, 0.015f)), V(0.41f, 0.5f), -18f), blush, 10f);
                c.Fill(Sdf.Rotate(Sdf.Ellipse(V(0.59f, 0.5f), V(0.034f, 0.015f)), V(0.59f, 0.5f), 18f), blush, 10f);
            }

            Mouth(c, look, expression);
        }

        /// <summary>Almond-shaped eye (the lens between two circles), tilted up at the outer corner.</summary>
        static void Eye(SdfCanvas c, AvatarLook look, Vector2 e, float side, bool closed, Color line)
        {
            bool f = look.Female;
            float halfW = f ? 0.032f : 0.029f, halfH = f ? 0.0165f : 0.0118f;
            float r = (halfW * halfW / halfH + halfH) * 0.5f, d = r - halfH;
            float tilt = f ? -side * 9f : -side * 4f;
            float lidAngle = Mathf.Asin(Mathf.Clamp01(halfW / r)) / Mathf.Deg2Rad;
            Vector2 outer = e + V(side * halfW, f ? 0.004f : 0.002f);

            if (closed)
            {
                c.Fill(Sdf.Arc(V(e.x, e.y + 0.022f), 0.034f, f ? 0.0045f : 0.0036f, 270f, 48f), line, 1.2f);
                if (f)
                    for (int i = 0; i < 3; i++)
                        c.Fill(Sdf.Taper(V(e.x + side * (0.008f + i * 0.01f), e.y - 0.011f + i * 0.001f),
                            V(e.x + side * (0.012f + i * 0.013f), e.y - 0.02f + i * 0.003f), 0.0022f, 0.0006f), line);
                return;
            }

            if (f)
            {
                // Eyeshadow
                Color shadow = Palette.Mix(look.Lips, Palette.Hex("4A1E5C"), 0.6f);
                c.Fill(Sdf.Rotate(Sdf.Ellipse(V(e.x + side * 0.006f, e.y + 0.012f), V(0.04f, 0.02f)), e, tilt), Palette.WithAlpha(shadow, 0.34f), 7f);
            }

            var white = Sdf.Rotate(Sdf.Intersect(Sdf.Circle(V(e.x, e.y - d), r), Sdf.Circle(V(e.x, e.y + d), r)), e, tilt);
            c.Fill(white, Palette.Hex("FBF7F4"));
            float ir = f ? 0.0158f : 0.0138f;
            Vector2 ic = V(e.x, e.y - 0.0015f);
            c.Fill(Sdf.Intersect(Sdf.Circle(ic, ir), white), Palette.Shade(look.Eyes, 0.7f));
            c.Fill(Sdf.Intersect(Sdf.Circle(ic, ir * 0.8f), white), look.Eyes);
            c.Fill(Sdf.Intersect(Sdf.Circle(ic + V(0f, -0.004f), ir * 0.55f), white), Palette.WithAlpha(Palette.Shade(look.Eyes, 1.35f), 0.6f), 3f);
            c.Fill(Sdf.Circle(ic, ir * 0.42f), Palette.Hex("100A0A"));
            c.Fill(Sdf.Intersect(Sdf.Circle(ic + V(0.005f, 0.006f), 0.0042f), white), Color.white);
            c.Fill(Sdf.Intersect(Sdf.Circle(ic + V(-0.005f, -0.006f), 0.0017f), white), new Color(1, 1, 1, 0.75f));
            // Upper lid shadow over the eyeball
            c.Paint(white, p => new Color(0.2f, 0.1f, 0.1f, Mathf.Clamp01((p.y - e.y) / halfH) * 0.18f));

            // Upper lid line (eyeliner for women, ending in a small wing)
            c.Fill(Sdf.Rotate(Sdf.Arc(V(e.x, e.y - d), r, f ? 0.0042f : 0.003f, 90f, lidAngle), e, tilt), line, 1.1f);
            if (f)
            {
                c.Fill(Sdf.Taper(outer, outer + V(side * 0.017f, 0.011f), 0.0036f, 0.0007f), line);
                for (int i = 0; i < 4; i++)
                {
                    float t = 0.35f + i * 0.2f;
                    Vector2 root = V(e.x + side * halfW * t, e.y + halfH * (1f - t * t) * 0.95f);
                    c.Fill(Sdf.Taper(root, root + V(side * (0.006f + i * 0.003f), 0.009f), 0.0018f, 0.0005f), line);
                }
            }
            // Lower lash line
            c.Fill(Sdf.Rotate(Sdf.Arc(V(e.x, e.y + d), r, 0.0014f, 270f, lidAngle * 0.8f), e, tilt), Palette.WithAlpha(line, f ? 0.45f : 0.3f), 1.2f);
            // Crease
            c.Fill(Sdf.Rotate(Sdf.Arc(V(e.x, e.y - d + 0.009f), r, 0.0014f, 90f, lidAngle * 0.75f), e, tilt), Palette.WithAlpha(Palette.Shade(look.Skin, 0.6f), 0.5f), 1.5f);
        }

        static void Mouth(SdfCanvas c, AvatarLook look, Expression expression)
        {
            bool f = look.Female;
            Color lip = f ? look.Lips : Palette.Mix(look.Skin, Palette.Hex("A0524A"), 0.45f);
            Color dark = Palette.Hex("4A1220");
            const float y = 0.448f;

            if (expression == Expression.Talk)
            {
                var open = Sdf.Ellipse(V(0.5f, y - 0.004f), V(f ? 0.026f : 0.028f, 0.017f));
                c.Fill(Sdf.Grow(open, f ? 0.007f : 0.004f), lip);
                c.Fill(open, dark);
                c.Fill(Sdf.Intersect(open, Sdf.HalfPlaneAbove(y + 0.002f)), Palette.Hex("FAFAF7"));
                c.Fill(Sdf.Intersect(open, Sdf.Ellipse(V(0.5f, y - 0.02f), V(0.016f, 0.01f))), Palette.Hex("C2455A"));
                return;
            }

            float skew = expression == Expression.Wink ? 0.006f : 0f;
            if (f)
            {
                // Full lips: a cupid's-bow upper lip, a fuller glossy lower lip, corners lifted into a smile.
                Color upper = Palette.Shade(lip, 0.86f);
                c.Fill(Sdf.Ellipse(V(0.5f, y - 0.007f), V(0.03f, 0.0135f)), lip);
                c.Fill(Sdf.Union(
                    Sdf.Rotate(Sdf.Ellipse(V(0.487f, y + 0.0055f), V(0.021f, 0.0085f)), V(0.487f, y + 0.0055f), -12f),
                    Sdf.Rotate(Sdf.Ellipse(V(0.513f, y + 0.0055f), V(0.021f, 0.0085f)), V(0.513f, y + 0.0055f), 12f)), upper);
                c.Fill(Sdf.Ellipse(V(0.507f, y - 0.01f), V(0.011f, 0.0042f)), new Color(1, 1, 1, 0.42f), 2f);
                c.Fill(Sdf.Arc(V(0.5f + skew, y + 0.03f), 0.034f, 0.0022f, 270f, 50f), Palette.Shade(lip, 0.5f), 1.1f);
                c.Fill(Sdf.Taper(V(0.468f + skew, y + 0.004f), V(0.462f + skew, y + 0.009f), 0.0022f, 0.001f), Palette.WithAlpha(Palette.Shade(lip, 0.55f), 0.8f));
                c.Fill(Sdf.Taper(V(0.532f + skew, y + 0.004f), V(0.538f + skew, y + 0.009f), 0.0022f, 0.001f), Palette.WithAlpha(Palette.Shade(lip, 0.55f), 0.8f));
            }
            else
            {
                // A confident half smile: one corner lifts higher.
                c.Fill(Sdf.Arc(V(0.5f + skew, y + 0.034f), 0.04f, 0.0032f, 272f, 44f), Palette.Shade(lip, 0.72f), 1.1f);
                c.Fill(Sdf.Taper(V(0.528f + skew, y + 0.006f), V(0.536f + skew, y + 0.012f), 0.0026f, 0.0012f), Palette.Shade(lip, 0.72f));
                c.Fill(Sdf.Ellipse(V(0.5f, y - 0.014f), V(0.022f, 0.0065f)), Palette.WithAlpha(Palette.Shade(look.Skin, 0.8f), 0.45f), 3f);
                c.Fill(Sdf.Ellipse(V(0.5f, y - 0.05f), V(0.03f, 0.008f)), Palette.WithAlpha(Palette.Shade(look.Skin, 0.82f), 0.35f), 6f);
            }
        }

        static void Earrings(SdfCanvas c, AvatarLook look)
        {
            c.Fill(Sdf.Circle(V(0.34f, 0.485f), 0.009f), look.Accent);
            c.Fill(Sdf.Circle(V(0.66f, 0.485f), 0.009f), look.Accent);
            c.Fill(Sdf.Circle(V(0.34f, 0.462f), 0.012f), Palette.Shade(look.Accent, 1.15f));
            c.Fill(Sdf.Circle(V(0.66f, 0.462f), 0.012f), Palette.Shade(look.Accent, 1.15f));
        }
    }
}
