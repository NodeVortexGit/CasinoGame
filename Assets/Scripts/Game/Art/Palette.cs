using UnityEngine;

namespace Casino.Art
{
    public static class Palette
    {
        public static readonly Color Gold = Hex("F5C542");
        public static readonly Color GoldLight = Hex("FFE9A3");
        public static readonly Color GoldDark = Hex("A8761C");
        public static readonly Color Ink = Hex("0D0A14");
        public static readonly Color Night = Hex("160A26");
        public static readonly Color Plum = Hex("2E1150");
        public static readonly Color Violet = Hex("5B2A9E");
        public static readonly Color Magenta = Hex("E0218A");
        public static readonly Color Pink = Hex("FF4FA3");
        public static readonly Color Cyan = Hex("35E0F0");
        public static readonly Color Red = Hex("E23B4E");
        public static readonly Color Green = Hex("27C26C");
        public static readonly Color GreenDark = Hex("12804A");
        public static readonly Color Blue = Hex("2F7BEA");
        public static readonly Color Orange = Hex("FF9A2E");
        public static readonly Color Text = Hex("FFFFFF");
        public static readonly Color TextMuted = Hex("C9BFDB");
        public static readonly Color TextDark = Hex("1B1225");
        public static readonly Color Panel = Hex("1E1233");
        public static readonly Color PanelLight = Hex("2C1B48");
        public static readonly Color Energy = Hex("3DF08F");
        public static readonly Color EnergyLow = Hex("FF4D5E");
        public static readonly Color CardRed = Hex("D0213B");
        public static readonly Color CardBlack = Hex("17131F");

        public static readonly Color[] ChipColors =
        {
            Hex("1E7BE0"), Hex("D8283A"), Hex("1F9D55"), Hex("23262E"), Hex("8A2BC2"), Hex("F08C14"),
        };

        public struct TableTheme
        {
            public Color Felt;
            public Color Accent;
            public string Label;
        }

        public static readonly TableTheme[] Tables =
        {
            new TableTheme { Felt = Hex("0D7A45"), Accent = Hex("D08A4A"), Label = "BRONZE" },
            new TableTheme { Felt = Hex("124F86"), Accent = Hex("D5DCE6"), Label = "SILVER" },
            new TableTheme { Felt = Hex("8A1730"), Accent = Hex("F5C542"), Label = "GOLD" },
            new TableTheme { Felt = Hex("43207A"), Accent = Hex("8DF3FF"), Label = "DIAMOND" },
        };

        public static Color Hex(string hex)
        {
            int v = System.Convert.ToInt32(hex, 16);
            float a = 1f;
            if (hex.Length == 8)
            {
                a = (v & 0xFF) / 255f;
                v >>= 8;
            }
            return new Color(((v >> 16) & 0xFF) / 255f, ((v >> 8) & 0xFF) / 255f, (v & 0xFF) / 255f, a);
        }

        /// <summary>Multiplies RGB only (Unity's Color * float would also scale alpha).</summary>
        public static Color Shade(Color c, float k) => new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a);

        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        public static Color Mix(Color a, Color b, float t) => new Color(
            a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t);
    }
}
