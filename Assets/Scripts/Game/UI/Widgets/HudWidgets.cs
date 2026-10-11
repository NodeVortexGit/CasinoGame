using System;
using Casino.Art;
using Casino.Core;
using Casino.Screens;
using UnityEngine;
using UnityEngine.UI;

namespace Casino.UI
{
    /// <summary>Chip balance with a count-up animation and a "+" that opens the cashier.</summary>
    public sealed class ChipCounter : MonoBehaviour
    {
        Text amount;
        double shown;
        long target;
        RectTransform iconRt;

        public static ChipCounter Create(Transform parent, float width = 380f)
        {
            var bg = UIKit.Panel(parent, "Chips", new Color(0, 0, 0, 0.45f), 42f, true);
            var rt = (RectTransform)bg.transform;
            rt.sizeDelta = new Vector2(width, 84);
            UIKit.Outline(rt, Palette.WithAlpha(Palette.Gold, 0.5f), 42f);
            var c = bg.gameObject.AddComponent<ChipCounter>();
            var icon = UIKit.Image(rt, "Coin", ProceduralArt.IconSprite(Icon.Coin), Color.white);
            c.iconRt = ((RectTransform)icon.transform).At(0f, 0.5f, -8, 0, 100, 100, 0f, 0.5f);
            c.amount = UIKit.Label(rt, "0", 48, Color.white, TextAnchor.MiddleLeft, FontKind.Display);
            ((RectTransform)c.amount.transform).Stretch(100, 0, 84, 0);
            var plus = UIKit.IconButton(rt, Icon.Plus, ButtonStyle.Green, () => App.I.UI.Modal<ShopModal>(m => m.StartTab = ShopTab.Chips), 72f);
            plus.Rect.At(1f, 0.5f, -6, 0, 72, 72, 1f, 0.5f);
            bg.gameObject.AddComponent<Button>().onClick.AddListener(() => App.I.UI.Modal<ShopModal>(m => m.StartTab = ShopTab.Chips));
            c.target = App.I.Wallet.Balance;
            c.shown = c.target;
            c.amount.text = ChipFormat.Full(c.target);
            App.I.Wallet.Changed += c.OnChanged;
            return c;
        }

        void OnChanged(long balance, long delta)
        {
            target = balance;
            if (delta > 0 && iconRt != null) StartCoroutine(Tween.Punch(iconRt, 0.25f, 0.35f));
        }

        void OnDestroy()
        {
            if (App.I != null) App.I.Wallet.Changed -= OnChanged;
        }

        void Update()
        {
            if (Math.Abs(shown - target) < 0.5) return;
            double diff = target - shown;
            shown += diff * Math.Min(1.0, Time.unscaledDeltaTime * 7.0) + Math.Sign(diff);
            if ((diff > 0 && shown > target) || (diff < 0 && shown < target)) shown = target;
            amount.text = ChipFormat.Full((long)Math.Round(shown));
        }
    }

    /// <summary>The character's energy: drains while playing, pulses red when low, "+" opens the bar menu.</summary>
    public sealed class EnergyBar : MonoBehaviour
    {
        Image fill, iconImg;
        Text label;
        float refresh;

        public static EnergyBar Create(Transform parent, float width = 360f)
        {
            var bg = UIKit.Panel(parent, "Energy", new Color(0, 0, 0, 0.45f), 42f, true);
            var rt = (RectTransform)bg.transform;
            rt.sizeDelta = new Vector2(width, 84);
            UIKit.Outline(rt, Palette.WithAlpha(Palette.Energy, 0.45f), 42f);
            var e = bg.gameObject.AddComponent<EnergyBar>();
            var icon = UIKit.Image(rt, "Bolt", ProceduralArt.IconSprite(Icon.Bolt), Palette.Energy);
            e.iconImg = icon;
            ((RectTransform)icon.transform).At(0f, 0.5f, 16, 0, 58, 58, 0f, 0.5f);
            var track = UIKit.Panel(rt, "Track", new Color(1, 1, 1, 0.12f), 16f);
            var trt = ((RectTransform)track.transform).Stretch(80, 24, 90, 24);
            e.fill = UIKit.Panel(trt, "Fill", Palette.Energy, 14f);
            var frt = (RectTransform)e.fill.transform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = new Vector2(1f, 1f);
            frt.pivot = new Vector2(0f, 0.5f);
            frt.offsetMin = frt.offsetMax = Vector2.zero;
            e.label = UIKit.Label(trt, "", 24, Color.white, TextAnchor.MiddleCenter, FontKind.Bold);
            ((RectTransform)e.label.transform).Stretch();
            UIKit.TextShadow(e.label, -2f, 0.8f);
            var plus = UIKit.IconButton(rt, Icon.Plus, ButtonStyle.Green, () => App.I.UI.Modal<ShopModal>(m => m.StartTab = ShopTab.Food), 72f);
            plus.Rect.At(1f, 0.5f, -6, 0, 72, 72, 1f, 0.5f);
            bg.gameObject.AddComponent<Button>().onClick.AddListener(() => App.I.UI.Modal<ShopModal>(m => m.StartTab = ShopTab.Food));
            e.Refresh();
            return e;
        }

        void Update()
        {
            refresh -= Time.unscaledDeltaTime;
            if (App.I.Energy.IsLow)
            {
                float pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f));
                fill.color = Palette.WithAlpha(Palette.EnergyLow, pulse);
                iconImg.color = Palette.WithAlpha(Palette.EnergyLow, pulse);
            }
            if (refresh <= 0f) Refresh();
        }

        void Refresh()
        {
            refresh = 0.5f;
            float v = App.I.Energy.Value;
            var frt = (RectTransform)fill.transform;
            frt.anchorMax = new Vector2(Mathf.Max(0.04f, v), 1f);
            fill.enabled = v > 0f;
            if (!App.I.Energy.IsLow)
            {
                fill.color = Palette.Energy;
                iconImg.color = Palette.Energy;
            }
            label.text = v <= 0f ? "EXHAUSTED" : Mathf.CeilToInt(v * 100f) + "%";
        }
    }

    /// <summary>Shared top bar: left action, title, chips, energy, settings.</summary>
    public sealed class TopBar : MonoBehaviour
    {
        public const float Height = 130f;

        public static TopBar Create(RectTransform parent, string title, Icon leftIcon, Action onLeft)
        {
            var bar = UIKit.NewRect("TopBar", parent);
            bar.At(0.5f, 1f, 0, 0, 1920, Height, 0.5f, 1f);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.sizeDelta = new Vector2(0, Height);
            var tb = bar.gameObject.AddComponent<TopBar>();

            // Touch targets are at least ~9 mm on a 6" phone (Android's 48dp guideline).
            var left = UIKit.IconButton(bar, leftIcon, ButtonStyle.Dark, onLeft, 96f);
            left.Rect.At(0f, 0.5f, 24, 0, 96, 96, 0f, 0.5f);
            if (!string.IsNullOrEmpty(title))
            {
                var t = UIKit.Label(bar, title, 50, Palette.Gold, TextAnchor.MiddleLeft, FontKind.Display, "Title");
                ((RectTransform)t.transform).At(0f, 0.5f, 140, 0, 480, 80, 0f, 0.5f);
                UIKit.TextShadow(t, -3f, 0.7f);
            }

            var settings = UIKit.IconButton(bar, Icon.Gear, ButtonStyle.Dark, () => App.I.UI.Modal<SettingsModal>(), 96f);
            settings.Rect.At(1f, 0.5f, -24, 0, 96, 96, 1f, 0.5f);
            var energy = EnergyBar.Create(bar);
            ((RectTransform)energy.transform).At(1f, 0.5f, -140, 0, 360, 84, 1f, 0.5f);
            var chips = ChipCounter.Create(bar);
            ((RectTransform)chips.transform).At(1f, 0.5f, -520, 0, 380, 84, 1f, 0.5f);
            var streak = StreakPill.Create(bar);
            ((RectTransform)streak.transform).At(1f, 0.5f, -916, 0, 150, 84, 1f, 0.5f);
            var level = LevelBadge.Create(bar);
            ((RectTransform)level.transform).At(1f, 0.5f, -1082, 0, 190, 84, 1f, 0.5f);
            return tb;
        }
    }

    /// <summary>Pill showing the Beginner's Luck boost while it lasts. Tapping it explains exactly what it does.</summary>
    public sealed class LuckBanner : MonoBehaviour
    {
        Text text;
        Image meter;
        CanvasGroup group;
        int lastHands = -1;

        public static LuckBanner Create(Transform parent)
        {
            var bg = UIKit.Panel(parent, "BeginnersLuck", Palette.Hex("1E5E2E"), 30f, true);
            var rt = (RectTransform)bg.transform;
            rt.sizeDelta = new Vector2(500, 64);
            UIKit.Outline(rt, Palette.Hex("7CFC9A"), 30f);
            var b = bg.gameObject.AddComponent<LuckBanner>();
            b.group = UIKit.Group(b);
            var star = UIKit.Image(rt, "Star", ProceduralArt.IconSprite(Icon.Star), Palette.Hex("B9FFC9"));
            ((RectTransform)star.transform).At(0f, 0.5f, 16, 2, 40, 40, 0f, 0.5f);
            b.text = UIKit.Label(rt, "", 22, Color.white, TextAnchor.MiddleLeft, FontKind.Semi);
            b.text.horizontalOverflow = HorizontalWrapMode.Overflow;
            ((RectTransform)b.text.transform).Stretch(66, 14, 16, 0);
            var track = UIKit.Panel(rt, "Track", new Color(0, 0, 0, 0.35f), 4f);
            var trt = ((RectTransform)track.transform).At(0f, 0f, 66, 10, 410, 8, 0f, 0f);
            b.meter = UIKit.Panel(trt, "Meter", Palette.Hex("7CFC9A"), 4f);
            var mrt = (RectTransform)b.meter.transform;
            mrt.anchorMin = Vector2.zero;
            mrt.pivot = new Vector2(0, 0.5f);
            mrt.offsetMin = mrt.offsetMax = Vector2.zero;
            bg.gameObject.AddComponent<Button>().onClick.AddListener(() => App.I.UI.Modal<LuckInfoModal>());
            b.Refresh();
            return b;
        }

        void Update()
        {
            if (App.I.Luck.HandsPlayed != lastHands) Refresh();
        }

        void Refresh()
        {
            var luck = App.I.Luck;
            lastHands = luck.HandsPlayed;
            gameObject.SetActive(luck.IsActive);
            if (!luck.IsActive) return;
            int pct = Mathf.RoundToInt((float)luck.Luck * 100f);
            text.text = $"BEGINNER'S LUCK {pct}%  <size=18><color=#B9FFC9>{luck.HandsRemaining} HANDS LEFT</color></size>";
            ((RectTransform)meter.transform).anchorMax = new Vector2(Mathf.Max(0.02f, (float)luck.Luck), 1f);
        }
    }
}
