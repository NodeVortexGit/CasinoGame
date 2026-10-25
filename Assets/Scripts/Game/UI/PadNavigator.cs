using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Casino.Art;
using Casino.Platform;
using Casino.Screens;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Casino.UI
{
    /// <summary>
    /// Controller navigation for the whole UI, built on what's already there: anything you can tap can be focused
    /// with the D-pad and pressed with confirm. Candidates are found by raycasting at their centre, so whatever is
    /// covered (by a modal, a fade, another panel) is skipped by itself, and buttons on the world-space table layers
    /// work the same as the HUD. Also draws the focus ring and the on-screen controller hints.
    /// </summary>
    public sealed class PadNavigator : MonoBehaviour
    {
        const float RepeatDelay = 0.4f, RepeatEvery = 0.11f, StickNav = 0.6f;

        struct Target
        {
            public GameObject Go;
            public Rect Rect;
        }

        UIRoot ui;
        GameObject focus;
        UIScreen layer;
        float defaultAt = -1f;
        Vector2Int held;
        float nextRepeat;
        RectTransform ring, badge, hints;
        PadFamily badgeFamily;
        string hintsKey;
        PointerEventData probe;
        readonly List<RaycastResult> hits = new List<RaycastResult>();
        readonly List<Target> targets = new List<Target>();
        readonly HashSet<GameObject> seen = new HashSet<GameObject>();
        readonly List<(PadGlyphKind, string)> rows = new List<(PadGlyphKind, string)>();
        static readonly Vector3[] Corners = new Vector3[4];

        public static PadNavigator I { get; private set; }
        public GameObject Focus => focus;

        internal void Init(UIRoot root)
        {
            I = this;
            ui = root;
        }

        void Update()
        {
            if (App.I == null || EventSystem.current == null) return;
            var top = ui.Top;
            bool walking = top != null && top == ui.Current && top.WalksWorld;
            if (top != layer)
            {
                // A modal opened or closed, or a new screen: focus its main button once it has settled in.
                layer = top;
                focus = null;
                defaultAt = walking ? -1f : Time.unscaledTime + 0.25f;
            }

            if (!Pad.Active)
            {
                focus = null;
                held = Vector2Int.zero;
                if (!walking) defaultAt = 0f;
                Draw(false, walking);
                return;
            }

            if (focus != null && !Usable(focus, out _))
            {
                focus = null;
                if (!walking) defaultAt = Time.unscaledTime;
            }
            if (focus == null && defaultAt >= 0f && Time.unscaledTime >= defaultAt)
            {
                focus = PickDefault(top);
                defaultAt = focus != null ? -1f : Time.unscaledTime + 0.3f;
            }

            var dir = Direction(!walking);
            if (dir != Vector2Int.zero)
            {
                if (focus == null) SetFocus(PickDefault(top));
                else if (dir.x != 0 && focus.TryGetComponent<Slider>(out var slider)) Nudge(slider, dir.x);
                else Move(dir);
            }

            if (Pad.Pressed(PadButton.Confirm))
            {
                if (focus != null) Press(focus);
                else top?.OnPadConfirm();
            }
            if (Pad.Pressed(PadButton.Back))
            {
                Pad.BackFrame = Time.frameCount;
                if (walking && focus != null) focus = null;   // back to walking
                else ui.Back();
            }
            if (Pad.Pressed(PadButton.Menu))
            {
                var open = ui.FindModal<SettingsModal>();
                if (open != null) open.Close();
                else if (!ui.HasModal && ui.Current != null && ui.Current.ShowsWorld) ui.Modal<SettingsModal>();
            }
            if (Pad.Pressed(PadButton.View) && walking) ui.Modal<DailyModal>();

            Draw(true, walking);
        }

        // ---------------- input ----------------

        /// <summary>A D-pad (or left stick, where it isn't walking) step, repeating while held.</summary>
        Vector2Int Direction(bool stickToo)
        {
            var d = Pad.Dpad;
            if (d == Vector2Int.zero && stickToo)
            {
                Vector2 s = Pad.Move;
                if (s.magnitude > StickNav)
                    d = Mathf.Abs(s.x) > Mathf.Abs(s.y) ? new Vector2Int(s.x > 0 ? 1 : -1, 0) : new Vector2Int(0, s.y > 0 ? 1 : -1);
            }
            if (d.x != 0 && d.y != 0) d.y = 0;
            if (d != held)
            {
                held = d;
                nextRepeat = Time.unscaledTime + RepeatDelay;
                return d;
            }
            if (d != Vector2Int.zero && Time.unscaledTime >= nextRepeat)
            {
                nextRepeat = Time.unscaledTime + RepeatEvery;
                return d;
            }
            return Vector2Int.zero;
        }

        void SetFocus(GameObject go)
        {
            if (go == null || go == focus) return;
            focus = go;
            Casino.Audio.Sfx.Play(Casino.Audio.SfxId.Tick);
        }

        static void Nudge(Slider s, int step)
        {
            s.normalizedValue = Mathf.Clamp01(s.normalizedValue + step * 0.05f);
            Casino.Audio.Sfx.Play(Casino.Audio.SfxId.Tick);
        }

        /// <summary>Presses like a finger would: down, click, and up a moment later so the button squashes visibly.</summary>
        void Press(GameObject go)
        {
            if (go.GetComponent<Slider>() != null || !Usable(go, out var r)) return;
            var e = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left, position = r.center, pressPosition = r.center, clickCount = 1,
            };
            ExecuteEvents.Execute(go, e, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(go, e, ExecuteEvents.pointerClickHandler);
            StartCoroutine(Release(go, e));
        }

        static IEnumerator Release(GameObject go, PointerEventData e)
        {
            yield return new WaitForSecondsRealtime(0.1f);
            if (go != null) ExecuteEvents.Execute(go, e, ExecuteEvents.pointerUpHandler);
        }

        // ---------------- finding things to focus ----------------

        void Gather()
        {
            targets.Clear();
            seen.Clear();
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (!(mb is IPointerClickHandler) && !(mb is Slider)) continue;
                if (!mb.isActiveAndEnabled || !seen.Add(mb.gameObject)) continue;
                if (Usable(mb.gameObject, out var r)) targets.Add(new Target { Go = mb.gameObject, Rect = r });
            }
        }

        /// <summary>On screen, enabled, not too big (backdrops) and actually the thing a tap at its centre would hit.</summary>
        bool Usable(GameObject go, out Rect rect)
        {
            rect = default;
            if (go == null || !go.activeInHierarchy || !(go.transform is RectTransform rt)) return false;
            var sel = go.GetComponent<Selectable>();
            if (sel != null && !sel.IsInteractable()) return false;
            var canvas = go.GetComponentInParent<Canvas>();
            if (canvas == null) return false;
            canvas = canvas.rootCanvas;
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay && cam == null) return false;
            if (canvas.renderMode == RenderMode.WorldSpace && !cam.isActiveAndEnabled) return false;   // a table layer while the room is hidden

            rect = ScreenRect(rt, cam);
            Vector2 view = ViewSize(cam);
            if (rect.width < 6f || rect.height < 6f || rect.width * rect.height > view.x * view.y * 0.4f) return false;
            Vector2 at = sel is Slider s && s.handleRect != null ? ScreenRect(s.handleRect, cam).center : rect.center;
            if (at.x < 0f || at.y < 0f || at.x > view.x || at.y > view.y) return false;

            probe ??= new PointerEventData(EventSystem.current);
            probe.position = at;
            hits.Clear();
            EventSystem.current.RaycastAll(probe, hits);
            return hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(go.transform);
        }

        /// <summary>Pixel size of what a camera draws into (the screen, or the dev tour's off-screen target).</summary>
        static Vector2 ViewSize(Camera cam) =>
            cam != null && cam.targetTexture != null ? new Vector2(cam.pixelWidth, cam.pixelHeight) : new Vector2(Screen.width, Screen.height);

        static Rect ScreenRect(RectTransform rt, Camera cam)
        {
            rt.GetWorldCorners(Corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = -min;
            for (int i = 0; i < 4; i++)
            {
                Vector2 p = RectTransformUtility.WorldToScreenPoint(cam, Corners[i]);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        /// <summary>The screen's own choice, else the main (gold or green) button nearest the lower middle.</summary>
        GameObject PickDefault(UIScreen top)
        {
            if (top == null) return null;
            var preferred = top.DefaultFocus;
            if (preferred != null && Usable(preferred, out _)) return preferred;
            Gather();
            GameObject best = null;
            float bestScore = float.MaxValue;
            var canvas = ui.Canvas;
            Vector2 view = ViewSize(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera);
            var aim = new Vector2(view.x * 0.5f, view.y * 0.3f);
            foreach (var t in targets)
            {
                var b = t.Go.GetComponent<UIButton>();
                bool main = b != null && (b.Style == ButtonStyle.Gold || b.Style == ButtonStyle.Green);
                float score = (t.Rect.center - aim).magnitude + (main ? 0f : view.y * 2f);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = t.Go;
                }
            }
            return best;
        }

        /// <summary>The nearest target that way: small gap along the move, and lined up with the current one if possible.</summary>
        void Move(Vector2Int d)
        {
            if (!Usable(focus, out var from))
            {
                SetFocus(PickDefault(layer));
                return;
            }
            Gather();
            Vector2 dir = d;
            GameObject best = null;
            float bestScore = float.MaxValue;
            foreach (var t in targets)
            {
                if (t.Go == focus) continue;
                Vector2 v = t.Rect.center - from.center;
                if (Vector2.Dot(v, dir) < 8f) continue;
                float gap, side, offAxis;
                if (d.x != 0)
                {
                    gap = d.x > 0 ? t.Rect.xMin - from.xMax : from.xMin - t.Rect.xMax;
                    side = Mathf.Max(t.Rect.yMin - from.yMax, from.yMin - t.Rect.yMax);
                    offAxis = Mathf.Abs(v.y);
                }
                else
                {
                    gap = d.y > 0 ? t.Rect.yMin - from.yMax : from.yMin - t.Rect.yMax;
                    side = Mathf.Max(t.Rect.xMin - from.xMax, from.xMin - t.Rect.xMax);
                    offAxis = Mathf.Abs(v.x);
                }
                float score = Mathf.Max(0f, gap) + Mathf.Max(0f, side) * 3f + offAxis * 0.1f + Mathf.Abs(Vector2.Dot(v, dir)) * 0.05f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = t.Go;
                }
            }
            SetFocus(best);
        }

        // ---------------- drawing ----------------

        void Draw(bool active, bool walking)
        {
            Rect r = default;
            bool show = active && focus != null && Usable(focus, out r);
            bool hintsOn = active && App.I.Save.padHints;
            if (ring == null) BuildRing();
            ring.gameObject.SetActive(show);
            if (show)
            {
                var canvas = ui.Canvas;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(ui.Overlay, r.center,
                    canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out var c);
                float k = 1f / Mathf.Max(0.01f, ui.Canvas.scaleFactor);
                ring.SetAsLastSibling();
                ring.localPosition = c;
                ring.sizeDelta = new Vector2(r.width * k + 24f, r.height * k + 24f);
                float pulse = 1f + 0.025f * Mathf.Sin(Time.unscaledTime * 6f);
                ring.localScale = new Vector3(pulse, pulse, 1f);
                badge.gameObject.SetActive(hintsOn && focus.GetComponent<Slider>() == null);
                if (badge.gameObject.activeSelf && badgeFamily != Pad.Family) DrawBadge();
            }
            if (hintsOn) Hints(walking);
            else if (hints != null) hints.gameObject.SetActive(false);
        }

        void BuildRing()
        {
            ring = UIKit.NewRect("PadFocus", ui.Overlay);
            ring.anchorMin = ring.anchorMax = new Vector2(0.5f, 0.5f);
            var glow = UIKit.Panel(ring, "Fill", new Color(1f, 0.92f, 0.6f, 0.1f), 26f);
            ((RectTransform)glow.transform).Stretch();
            UIKit.Outline(ring, Palette.GoldLight, 26f);
            var inner = UIKit.Outline(ring, new Color(1, 1, 1, 0.55f), 22f);
            ((RectTransform)inner.transform).Stretch(5, 5, 5, 5);
            // The confirm button's glyph on the top-left corner, which stays on screen for the HUD's edge buttons too.
            badge = UIKit.NewRect("Badge", ring);
            badge.anchorMin = badge.anchorMax = new Vector2(0f, 1f);
            badge.sizeDelta = new Vector2(52, 52);
            badge.anchoredPosition = new Vector2(6, -6);
            DrawBadge();
            ring.gameObject.SetActive(false);
        }

        void DrawBadge()
        {
            for (int i = badge.childCount - 1; i >= 0; i--) Destroy(badge.GetChild(i).gameObject);
            badgeFamily = Pad.Family;
            PadGlyph.Create(badge, PadGlyphKind.Confirm, 52f, true);
        }

        /// <summary>What the buttons do right now, listed down the left edge (clear of every screen's own controls).</summary>
        void Hints(bool walking)
        {
            rows.Clear();
            if (walking && focus == null)
            {
                rows.Add((PadGlyphKind.LeftStick, Loc.T("Walk")));
                rows.Add((PadGlyphKind.RightStick, Loc.T("Look")));
                string use = layer.PadConfirmHint;
                if (!string.IsNullOrEmpty(use)) rows.Add((PadGlyphKind.Confirm, CultureInfo.InvariantCulture.TextInfo.ToTitleCase(use.ToLowerInvariant())));
                rows.Add((PadGlyphKind.Dpad, Loc.T("Buttons")));
                rows.Add((PadGlyphKind.Menu, Loc.T("Settings")));
                rows.Add((PadGlyphKind.View, Loc.T("Daily")));
            }
            else
            {
                bool slider = focus != null && focus.GetComponent<Slider>() != null;
                rows.Add((PadGlyphKind.Dpad, slider ? Loc.T("Adjust") : Loc.T("Move")));
                if (!slider) rows.Add((PadGlyphKind.Confirm, Loc.T("Select")));
                rows.Add((PadGlyphKind.Back, walking ? Loc.T("Walk") : layer is UIModal ? Loc.T("Close") : Loc.T("Back")));
                if (!(layer is UIModal) && layer != null && layer.ShowsWorld) rows.Add((PadGlyphKind.Menu, Loc.T("Settings")));
            }

            var key = new StringBuilder().Append((int)Pad.Family).Append(App.I.Save.padSwapMenuView);
            foreach (var (kind, text) in rows) key.Append('|').Append((int)kind).Append(text);
            string k = key.ToString();
            if (hints != null && k == hintsKey)
            {
                hints.gameObject.SetActive(true);
                return;
            }
            hintsKey = k;
            if (hints != null) Destroy(hints.gameObject);

            const float RowH = 58f, Glyph = 46f;
            var bg = UIKit.Panel(ui.Overlay, "PadHints", new Color(0.04f, 0.02f, 0.08f, 0.62f), 26f);
            hints = (RectTransform)bg.transform;
            float h = rows.Count * RowH + 20f;
            hints.At(0f, 0.5f, 16, 0, 220, h, 0f, 0.5f);
            float width = 0f;
            for (int i = 0; i < rows.Count; i++)
            {
                float y = h * 0.5f - 10f - RowH * (i + 0.5f);
                var g = PadGlyph.Create(hints, rows[i].Item1, Glyph);
                g.anchorMin = g.anchorMax = new Vector2(0f, 0.5f);
                g.pivot = new Vector2(0f, 0.5f);
                g.anchoredPosition = new Vector2(14f, y);
                var t = UIKit.Label(hints, rows[i].Item2, 26, Color.white, TextAnchor.MiddleLeft, FontKind.Semi);
                var trt = ((RectTransform)t.transform).At(0f, 0.5f, 22f + Mathf.Max(Glyph, g.sizeDelta.x), y, 300, RowH, 0f, 0.5f);
                width = Mathf.Max(width, trt.anchoredPosition.x + t.preferredWidth + 18f);
            }
            hints.sizeDelta = new Vector2(Mathf.Max(170f, width), h);
        }
    }

    public enum PadGlyphKind { Confirm, Back, Menu, View, LeftStick, RightStick, Dpad }

    /// <summary>
    /// A controller button as drawn on that controller: Xbox's coloured A/B/X/Y, PlayStation's cross, circle, square
    /// and triangle, Nintendo's A/B/X/Y (A on the right), plus the menu buttons, sticks and D-pad.
    /// </summary>
    public static class PadGlyph
    {
        static readonly Color Back = new Color(0.1f, 0.08f, 0.14f, 0.95f);

        public static RectTransform Create(Transform parent, PadGlyphKind kind, float size, bool fill = false)
        {
            var f = Pad.Family;
            RectTransform rt;
            switch (kind)
            {
                case PadGlyphKind.Menu:
                case PadGlyphKind.View:
                {
                    string text = Pad.Name(kind == PadGlyphKind.Menu ? PadButton.Menu : PadButton.View).ToUpperInvariant();
                    var bg = UIKit.Panel(parent, "Glyph", Back, size * 0.3f);
                    rt = (RectTransform)bg.transform;
                    UIKit.Outline(rt, new Color(1, 1, 1, 0.5f), size * 0.3f);
                    // Nintendo's + and - are single characters: draw them bigger than MENU / OPTIONS.
                    var t = UIKit.Label(rt, text, Mathf.RoundToInt(size * (text.Length == 1 ? 0.7f : 0.42f)), Color.white, TextAnchor.MiddleCenter, FontKind.Display);
                    ((RectTransform)t.transform).Stretch();
                    rt.sizeDelta = new Vector2(Mathf.Max(size, t.preferredWidth + size * 0.5f), size * 0.72f);
                    break;
                }
                case PadGlyphKind.Dpad:
                {
                    var bg = UIKit.Panel(parent, "Glyph", Back, size * 0.25f);
                    rt = (RectTransform)bg.transform;
                    rt.sizeDelta = new Vector2(size, size);
                    var plus = UIKit.Image(rt, "Dpad", ProceduralArt.IconSprite(Icon.Plus), Color.white);
                    ((RectTransform)plus.transform).Stretch(size * 0.12f, size * 0.12f, size * 0.12f, size * 0.12f);
                    break;
                }
                case PadGlyphKind.LeftStick:
                case PadGlyphKind.RightStick:
                {
                    var bg = UIKit.Image(parent, "Glyph", ProceduralArt.Circle, Back);
                    rt = (RectTransform)bg.transform;
                    rt.sizeDelta = new Vector2(size, size);
                    var ringImg = UIKit.Image(rt, "Ring", ProceduralArt.Ring, new Color(1, 1, 1, 0.6f));
                    ((RectTransform)ringImg.transform).Stretch(size * 0.08f, size * 0.08f, size * 0.08f, size * 0.08f);
                    var t = UIKit.Label(rt, kind == PadGlyphKind.LeftStick ? "L" : "R", Mathf.RoundToInt(size * 0.5f), Color.white,
                        TextAnchor.MiddleCenter, FontKind.Display);
                    ((RectTransform)t.transform).Stretch();
                    break;
                }
                default:
                {
                    var face = Pad.FaceOf(kind == PadGlyphKind.Confirm ? PadButton.Confirm : PadButton.Back);
                    var bg = UIKit.Image(parent, "Glyph", ProceduralArt.Circle, Back);
                    rt = (RectTransform)bg.transform;
                    rt.sizeDelta = new Vector2(size, size);
                    Color c = FaceColor(face, f);
                    if (f == PadFamily.PlayStation)
                    {
                        var icon = face == Pad.Face.South ? Icon.PadCross : face == Pad.Face.East ? Icon.PadCircle
                            : face == Pad.Face.West ? Icon.PadSquare : Icon.PadTriangle;
                        var img = UIKit.Image(rt, "Shape", ProceduralArt.IconSprite(icon), c);
                        ((RectTransform)img.transform).Stretch(size * 0.14f, size * 0.14f, size * 0.14f, size * 0.14f);
                    }
                    else
                    {
                        var t = UIKit.Label(rt, Pad.FaceName(face, f), Mathf.RoundToInt(size * 0.62f), c, TextAnchor.MiddleCenter, FontKind.Display);
                        ((RectTransform)t.transform).Stretch();
                    }
                    break;
                }
            }
            if (fill) rt.Stretch();
            return rt;
        }

        static Color FaceColor(Pad.Face face, PadFamily f)
        {
            switch (f)
            {
                case PadFamily.PlayStation:
                    return Palette.Hex(face == Pad.Face.South ? "7FB0EA" : face == Pad.Face.East ? "FF6F61" : face == Pad.Face.West ? "E58AD6" : "3FD1A4");
                case PadFamily.Nintendo:
                    return Color.white;
                default:
                    return Palette.Hex(face == Pad.Face.South ? "6CCB2E" : face == Pad.Face.East ? "F04B42" : face == Pad.Face.West ? "3A8EF0" : "F2C318");
            }
        }
    }
}
