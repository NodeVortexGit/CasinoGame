using System;
using System.Collections.Generic;
using Casino.Core.Blackjack;
using Casino.Core.Player;
using UnityEngine;

namespace Casino.Art
{
    /// <summary>
    /// Generates and caches every sprite the game uses. Nothing is shipped as an image except optional overrides:
    /// put PNGs in Resources/Dealers/Female or Resources/Dealers/Male to replace the generated staff portraits.
    /// </summary>
    public static class ProceduralArt
    {
        /// <summary>Corner radius (in sprite pixels) of <see cref="RoundRect"/>; see UIKit.Rounded.</summary>
        public const float RoundRadiusPx = 28f;

        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        static Texture2D[] femaleOverrides, maleOverrides;

        public static Sprite RoundRect => Get("roundrect", Painters.RoundRect, 30);
        public static Sprite RoundOutline => Get("roundoutline", () => Painters.RoundRectOutline(4f), 30);
        public static Sprite SoftShadow => Get("softshadow", Painters.SoftShadow, 30);
        public static Sprite Circle => Get("circle", () => Painters.Circle());
        public static Sprite Ring => Get("ring", () => Painters.Ring(0.05f));
        public static Sprite ThinRing => Get("thinring", () => Painters.Ring(0.004f, 512));
        public static Sprite DashedRing => Get("dashedring", () => Painters.DashedRing(28, 0.018f));
        public static Sprite Glow => Get("glow", () => Painters.Glow());
        public static Sprite GradientV => Get("gradientv", Painters.GradientV);
        public static Sprite Vignette => Get("vignette", () => Painters.Vignette(0.85f));
        public static Sprite Sunburst => Get("sunburst", () => Painters.Sunburst());
        public static Sprite Sparkle => Get("sparkle", Painters.Sparkle);
        public static Sprite Bulb => Get("bulb", Painters.Bulb);
        public static Sprite BubbleTail => Get("bubbletail", Painters.BubbleTail);
        public static Sprite CardFace => Get("cardface", Painters.CardFace, 16);

        public static Sprite CardBack => Get("cardback", () => Painters.CardBack(Palette.Hex("B3122E")));
        public static Sprite Suit(Suit suit) => Get("suit" + (int)suit, () => Painters.Suit(suit));
        public static Sprite Chip(int colorIndex) =>
            Get("chip" + colorIndex, () => Painters.Chip(Palette.ChipColors[Mathf.Abs(colorIndex) % Palette.ChipColors.Length]));
        public static Sprite IconSprite(Icon icon) => Get("icon" + icon, () => Painters.IconImage(icon));
        public static Sprite Food(string productId) => Get("food" + productId, () => Painters.FoodImage(productId));
        public static Sprite Felt(int theme) => Get("felt" + theme, () => Painters.Felt(Palette.Tables[theme % Palette.Tables.Length].Felt));

        public static Sprite SlotSymbol(Casino.Core.Slots.SlotSymbol s) => Get("slot" + s, () => GamePainters.SlotSymbolImage(s, 160));
        public static Sprite RouletteWheel => Get("roulettewheel", () => GamePainters.RouletteWheel(512));
        public static Sprite Ball => Get("ball", () => GamePainters.Ball(48));

        public static Sprite Gradient(string key, Color top, Color bottom) =>
            Get("grad" + key, () => Painters.VerticalGradient(top, bottom));

        /// <summary>Staff portrait. A matching override image in Resources/Dealers wins over the generated one.</summary>
        public static Sprite Portrait(bool female, int seed, StaffRole role, Expression expression)
        {
            var overrides = Overrides(female);
            if (overrides.Length > 0)
            {
                var tex = overrides[Mathf.Abs(seed + (int)role * 3) % overrides.Length];
                return Get("override" + tex.GetInstanceID(), null, 0, tex);
            }
            string key = $"portrait{(female ? 'f' : 'm')}{seed}_{role}_{expression}";
            return Get(key, () => AvatarPainter.Paint(AvatarPainter.LookFor(female, seed, role), expression, 256));
        }

        static Texture2D[] Overrides(bool female)
        {
            if (female) return femaleOverrides ??= Resources.LoadAll<Texture2D>("Dealers/Female");
            return maleOverrides ??= Resources.LoadAll<Texture2D>("Dealers/Male");
        }

        static Sprite Get(string key, Func<SdfCanvas> paint, float border = 0, Texture2D existing = null)
        {
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            Texture2D tex = existing;
            if (tex == null)
            {
                var canvas = paint();
                tex = canvas.ToTexture(key);
            }
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            sprite.name = key;
            Cache[key] = sprite;
            return sprite;
        }
    }
}
