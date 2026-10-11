using System.Collections;
using System.Collections.Generic;
using Casino.Art;
using Casino.Audio;
using Casino.Screens;
using UnityEngine;
using UnityEngine.UI;

namespace Casino.UI
{
    /// <summary>
    /// The daily streak in the top bar: a flame and the number of days. The flame lights up once today's goal is met
    /// and stays grey until then; tapping opens the daily screen.
    /// </summary>
    public sealed class StreakPill : MonoBehaviour
    {
        public static readonly Color Fire = Palette.Hex("FF8A1F");
        Image flame;
        Text count;
        RectTransform flameRt;
        bool lit;

        public static StreakPill Create(Transform parent)
        {
            var bg = UIKit.Panel(parent, "Streak", new Color(0, 0, 0, 0.45f), 42f, true);
            var rt = (RectTransform)bg.transform;
            rt.sizeDelta = new Vector2(150, 84);
            UIKit.Outline(rt, Palette.WithAlpha(Fire, 0.5f), 42f);
            var s = bg.gameObject.AddComponent<StreakPill>();
            s.flame = UIKit.Image(rt, "Flame", ProceduralArt.IconSprite(Icon.Flame), Fire);
            s.flameRt = ((RectTransform)s.flame.transform).At(0f, 0.5f, 10, 2, 68, 68, 0f, 0.5f);
            s.count = UIKit.Label(rt, "0", 46, Color.white, TextAnchor.MiddleCenter, FontKind.Display);
            ((RectTransform)s.count.transform).Stretch(70, 0, 12, 0);
            bg.gameObject.AddComponent<Button>().onClick.AddListener(() => App.I.UI.Modal<DailyModal>());
            App.I.Progress.Changed += s.Refresh;
            s.Refresh();
            return s;
        }

        void OnDestroy()
        {
            if (App.I != null) App.I.Progress.Changed -= Refresh;
        }

        void Refresh()
        {
            var p = App.I.Progress;
            lit = p.Streak.DoneToday(p.Day);
            count.text = p.Streak.Count.ToString();
            flame.color = lit ? Fire : new Color(0.55f, 0.52f, 0.6f, 0.9f);
            count.color = lit ? Color.white : Palette.TextMuted;
        }

        void Update()
        {
            // A lit flame flickers a little.
            float k = lit ? 1f + Mathf.Sin(Time.unscaledTime * 9f) * 0.03f + Mathf.Sin(Time.unscaledTime * 23f) * 0.02f : 1f;
            flameRt.localScale = new Vector3(1f, k, 1f);
        }
    }

    /// <summary>Player level with the progress towards the next one; tapping opens the daily screen.</summary>
    public sealed class LevelBadge : MonoBehaviour
    {
        Text number;
        RectTransform fill;

        public static LevelBadge Create(Transform parent)
        {
            var bg = UIKit.Panel(parent, "Level", new Color(0, 0, 0, 0.45f), 42f, true);
            var rt = (RectTransform)bg.transform;
            rt.sizeDelta = new Vector2(190, 84);
            UIKit.Outline(rt, Palette.WithAlpha(Palette.Gold, 0.5f), 42f);
            var b = bg.gameObject.AddComponent<LevelBadge>();
            var star = UIKit.Image(rt, "Star", ProceduralArt.IconSprite(Icon.Star), Palette.Gold);
            ((RectTransform)star.transform).At(0f, 0.5f, 6, 0, 78, 78, 0f, 0.5f);
            b.number = UIKit.Label(star.transform, "1", 30, Palette.TextDark, TextAnchor.MiddleCenter, FontKind.Display);
            ((RectTransform)b.number.transform).Stretch(0, 0, 0, 4);
            var label = UIKit.Label(rt, "LEVEL", 22, Palette.GoldLight, TextAnchor.MiddleLeft, FontKind.Semi);
            ((RectTransform)label.transform).At(0f, 0.5f, 90, 14, 90, 30, 0f, 0.5f);
            var track = UIKit.Panel(rt, "Track", new Color(1, 1, 1, 0.15f), 6f);
            var trt = ((RectTransform)track.transform).At(0f, 0.5f, 90, -14, 82, 12, 0f, 0.5f);
            var f = UIKit.Panel(trt, "Fill", Palette.Gold, 6f);
            b.fill = (RectTransform)f.transform;
            b.fill.anchorMin = Vector2.zero;
            b.fill.pivot = new Vector2(0f, 0.5f);
            b.fill.offsetMin = b.fill.offsetMax = Vector2.zero;
            bg.gameObject.AddComponent<Button>().onClick.AddListener(() => App.I.UI.Modal<DailyModal>());
            App.I.Progress.Changed += b.Refresh;
            b.Refresh();
            return b;
        }

        void OnDestroy()
        {
            if (App.I != null) App.I.Progress.Changed -= Refresh;
        }

        void Refresh()
        {
            var p = App.I.Progress;
            number.text = p.Level.ToString();
            number.fontSize = p.Level >= 100 ? 22 : 30;
            fill.anchorMax = new Vector2(Mathf.Max(0.06f, p.LevelProgress), 1f);
        }
    }

    /// <summary>Big banner under the top bar for level-ups, streaks and missions. Queued, so several never overlap.</summary>
    public sealed class CelebrationBanner : MonoBehaviour
    {
        static readonly Queue<Celebration> Waiting = new Queue<Celebration>();
        static CelebrationBanner current;

        public static void Show(RectTransform overlay, Celebration c)
        {
            Waiting.Enqueue(c);
            if (current == null) Next(overlay);
        }

        static void Next(RectTransform overlay)
        {
            if (Waiting.Count == 0 || overlay == null) return;
            var c = Waiting.Dequeue();
            var bg = UIKit.Panel(overlay, "Celebration", new Color(0.07f, 0.03f, 0.12f, 0.95f), 40f);
            var rt = ((RectTransform)bg.transform).At(0.5f, 1f, 0, -TopBar.Height - 16, 1020, 150, 0.5f, 1f);
            UIKit.Outline(rt, c.Color, 40f);
            var glow = UIKit.Image(rt, "Glow", ProceduralArt.Glow, Palette.WithAlpha(c.Color, 0.45f));
            ((RectTransform)glow.transform).At(0f, 0.5f, -40, 0, 260, 260, 0f, 0.5f);
            var icon = UIKit.Image(rt, "Icon", ProceduralArt.IconSprite(c.Icon), c.Color);
            ((RectTransform)icon.transform).At(0f, 0.5f, 26, 0, 110, 110, 0f, 0.5f);
            var title = UIKit.Label(rt, c.Title, 62, c.Color, TextAnchor.MiddleLeft, FontKind.Display);
            ((RectTransform)title.transform).Stretch(160, 58, 30, 8);
            UIKit.TextShadow(title, -3f, 0.7f);
            var sub = UIKit.Label(rt, c.Subtitle, 28, Color.white, TextAnchor.MiddleLeft, FontKind.Semi);
            ((RectTransform)sub.transform).Stretch(160, 12, 30, 92);
            current = bg.gameObject.AddComponent<CelebrationBanner>();
            current.StartCoroutine(current.Play(overlay, (RectTransform)icon.transform));
        }

        IEnumerator Play(RectTransform overlay, RectTransform icon)
        {
            var group = UIKit.Group(this);
            group.blocksRaycasts = false;
            group.alpha = 0f;
            transform.localScale = Vector3.one * 0.6f;
            Sfx.Play(SfxId.Ding);
            StartCoroutine(Tween.Fade(group, 1f, 0.15f));
            yield return Tween.Scale(transform, Vector3.one, 0.35f, Ease.OutBack);
            StartCoroutine(Tween.Punch(icon, 0.25f, 0.4f));
            yield return Tween.Wait(2.4f);
            yield return Tween.Fade(group, 0f, 0.3f);
            current = null;
            Destroy(gameObject);
            Next(overlay);
        }
    }
}
