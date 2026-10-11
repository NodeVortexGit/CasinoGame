using System;
using Casino.Art;
using UnityEngine;
using UnityEngine.UI;

namespace Casino.UI
{
    public enum ButtonStyle { Gold, Green, Red, Purple, Blue, Dark, Ghost }

    /// <summary>Code-only uGUI construction helpers. Layout is in reference pixels of a 1920x1080 canvas.</summary>
    public static class UIKit
    {
        public static RectTransform NewRect(string name, Transform parent)
        {
            // Children take their parent's layer, so UI built inside the 3D casino (game tables) is drawn by its camera.
            var go = new GameObject(name, typeof(RectTransform)) { layer = parent != null ? parent.gameObject.layer : 5 };
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Anchor at a single point of the parent (0..1) and place at an offset from it.</summary>
        public static RectTransform At(this RectTransform rt, float anchorX, float anchorY, float x, float y, float w, float h,
            float pivotX = 0.5f, float pivotY = 0.5f)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(anchorX, anchorY);
            rt.pivot = new Vector2(pivotX, pivotY);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);
            return rt;
        }

        /// <summary>Centred placement (anchor = parent centre).</summary>
        public static RectTransform Center(this RectTransform rt, float x, float y, float w, float h) => rt.At(0.5f, 0.5f, x, y, w, h);

        public static RectTransform Stretch(this RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static Image Image(Transform parent, string name, Sprite sprite, Color color, bool raycast = false)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static Image Panel(Transform parent, string name, Color color, float radius, bool raycast = false)
        {
            var img = Image(parent, name, null, color, raycast);
            Rounded(img, radius);
            return img;
        }

        public static void Rounded(Image img, float radius)
        {
            img.sprite = ProceduralArt.RoundRect;
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = ProceduralArt.RoundRadiusPx / Mathf.Max(1f, radius);
        }

        public static Image Outline(Transform parent, Color color, float radius)
        {
            var img = Image(parent, "Outline", ProceduralArt.RoundOutline, color);
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = ProceduralArt.RoundRadiusPx / Mathf.Max(1f, radius);
            ((RectTransform)img.transform).Stretch();
            return img;
        }

        /// <summary>Blurred drop shadow behind a rounded panel; add it before the panel itself.</summary>
        public static Image DropShadow(Transform parent, RectTransform target, float spread = 18f, float offsetY = -10f, float alpha = 0.55f)
        {
            var img = Image(parent, "Shadow", ProceduralArt.SoftShadow, new Color(0, 0, 0, alpha));
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 0.6f;
            var rt = (RectTransform)img.transform;
            rt.anchorMin = target.anchorMin;
            rt.anchorMax = target.anchorMax;
            rt.pivot = target.pivot;
            rt.anchoredPosition = target.anchoredPosition + new Vector2(0, offsetY);
            rt.sizeDelta = target.sizeDelta + new Vector2(spread * 2, spread * 2);
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter,
            FontKind font = FontKind.Body, string name = "Label")
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Fonts.Get(font);
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.text = text;
            t.raycastTarget = false;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 1f;
            return t;
        }

        public static Shadow TextShadow(Graphic g, float dy = -3f, float alpha = 0.6f)
        {
            var s = g.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0, 0, 0, alpha);
            s.effectDistance = new Vector2(0, dy);
            return s;
        }

        public static Outline TextOutline(Graphic g, Color color, float size = 2f)
        {
            var o = g.gameObject.AddComponent<UnityEngine.UI.Outline>();
            o.effectColor = color;
            o.effectDistance = new Vector2(size, -size);
            return o;
        }

        public static Color StyleColor(ButtonStyle style)
        {
            switch (style)
            {
                case ButtonStyle.Gold: return Palette.Hex("F2B632");
                case ButtonStyle.Green: return Palette.Hex("1FB25A");
                case ButtonStyle.Red: return Palette.Hex("D9334A");
                case ButtonStyle.Purple: return Palette.Hex("7B3FD6");
                case ButtonStyle.Blue: return Palette.Hex("2C7BE5");
                case ButtonStyle.Dark: return Palette.Hex("2B1F3D");
                default: return new Color(1, 1, 1, 0.08f);
            }
        }

        public static UIButton Button(Transform parent, string label, ButtonStyle style, Action onClick, int fontSize = 40,
            Icon? icon = null, FontKind font = FontKind.Display, float radius = 24f)
        {
            var rt = NewRect("Button " + label, parent);
            var b = rt.gameObject.AddComponent<UIButton>();
            b.Build(label, style, onClick, fontSize, icon, font, radius);
            return b;
        }

        /// <summary>Round icon-only button (settings gear, close X, plus).</summary>
        public static UIButton IconButton(Transform parent, Icon icon, ButtonStyle style, Action onClick, float size = 84f)
        {
            var rt = NewRect("IconButton " + icon, parent);
            rt.sizeDelta = new Vector2(size, size);
            var b = rt.gameObject.AddComponent<UIButton>();
            b.Build(null, style, onClick, 0, icon, FontKind.Display, size / 2f);
            return b;
        }

        /// <summary>
        /// Gives a constantly animated element its own nested canvas so moving it doesn't re-batch the whole UI every
        /// frame (a big saving on low-end phones). Interactive elements also need their own raycaster.
        /// </summary>
        public static void OwnCanvas(GameObject go, bool interactive)
        {
            if (go.GetComponent<Canvas>() == null) go.AddComponent<Canvas>();
            if (interactive && go.GetComponent<GraphicRaycaster>() == null) go.AddComponent<GraphicRaycaster>();
        }

        public static CanvasGroup Group(Component c)
        {
            var g = c.GetComponent<CanvasGroup>();
            return g != null ? g : c.gameObject.AddComponent<CanvasGroup>();
        }

        /// <summary>A transparent full-rect raycast blocker (for modal backdrops and tap-to-continue).</summary>
        public static Image Blocker(Transform parent, Color color)
        {
            var img = Image(parent, "Blocker", null, color, true);
            ((RectTransform)img.transform).Stretch();
            return img;
        }
    }
}
