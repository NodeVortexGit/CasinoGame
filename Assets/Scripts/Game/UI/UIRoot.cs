using System;
using System.Collections;
using System.Collections.Generic;
using Casino.Art;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Casino.UI
{
    /// <summary>Base for full-screen views. <see cref="Root"/> is full-bleed, <see cref="Content"/> respects the safe area.</summary>
    public abstract class UIScreen : MonoBehaviour
    {
        public RectTransform Root { get; private set; }
        public RectTransform Content { get; private set; }

        internal void Init()
        {
            Root = (RectTransform)transform;
            Root.Stretch();
            Content = UIKit.NewRect("Content", Root);
            Content.Stretch();
            Content.gameObject.AddComponent<SafeArea>();
            Build();
            // Backgrounds are added to Root during Build, after Content was created; keep Content drawn on top of them.
            Content.SetAsLastSibling();
        }

        /// <summary>Create the view's hierarchy. Backgrounds go on Root, interactive layout on Content.</summary>
        protected abstract void Build();

        /// <summary>Android back / Escape. Return true when handled.</summary>
        public virtual bool OnBack() => false;

        /// <summary>True for screens drawn over the 3D casino (the lobby, and games played at their table in the room).</summary>
        public virtual bool ShowsWorld => false;
    }

    /// <summary>Dialog on a dimmed backdrop. Close with <see cref="Close"/>.</summary>
    public abstract class UIModal : UIScreen
    {
        public RectTransform Panel { get; protected set; }
        public event Action Closed;
        public bool CloseOnBackdrop = true;
        bool closing;

        protected Image Backdrop { get; private set; }

        internal void InitModal()
        {
            Backdrop = UIKit.Blocker(transform, new Color(0.03f, 0.01f, 0.06f, 0.78f));
            Backdrop.gameObject.AddComponent<Button>().onClick.AddListener(() => { if (CloseOnBackdrop) Close(); });
            Init();
        }

        /// <summary>Standard panel with drop shadow, rounded corners and a gold rim, centred in the safe area.</summary>
        protected RectTransform MakePanel(float w, float h, string title = null)
        {
            var shadowHost = UIKit.NewRect("PanelShadow", Content).Center(0, 0, w, h);
            UIKit.DropShadow(Content, shadowHost, 30f, -16f, 0.7f);
            Destroy(shadowHost.gameObject);
            var bg = UIKit.Panel(Content, "Panel", Palette.Panel, 36f, true);
            Panel = ((RectTransform)bg.transform).Center(0, 0, w, h);
            var top = UIKit.Image(Panel, "Sheen", ProceduralArt.GradientV, new Color(1, 1, 1, 0.06f));
            ((RectTransform)top.transform).Stretch(10, 0, 10, 8).anchorMin = new Vector2(0, 0.6f);
            UIKit.Outline(Panel, Palette.WithAlpha(Palette.Gold, 0.55f), 36f);
            if (title != null)
            {
                var t = UIKit.Label(Panel, title, 64, Palette.Gold, TextAnchor.MiddleCenter, FontKind.Display, "Title");
                ((RectTransform)t.transform).At(0.5f, 1f, 0, -62, w - 200, 90);
                UIKit.TextShadow(t, -4f, 0.7f);
            }
            return Panel;
        }

        protected UIButton AddCloseButton()
        {
            var b = UIKit.IconButton(Panel, Icon.Close, ButtonStyle.Dark, Close, 92f);
            b.Rect.At(1f, 1f, -22, -22, 92, 92, 1f, 1f);
            return b;
        }

        public override bool OnBack()
        {
            Close();
            return true;
        }

        public void Close()
        {
            if (closing) return;
            closing = true;
            App.I.UI.CloseModal(this);
            Closed?.Invoke();
        }
    }

    /// <summary>Owns the canvas, the current screen, the modal stack and toasts.</summary>
    public sealed class UIRoot : MonoBehaviour
    {
        RectTransform screenLayer, modalLayer, overlayLayer;
        readonly List<UIModal> modals = new List<UIModal>();

        public Canvas Canvas { get; private set; }
        public UIScreen Current { get; private set; }
        public RectTransform Overlay => overlayLayer;
        public bool HasModal => modals.Count > 0;

        public static UIRoot Create(Transform parent)
        {
            var go = new GameObject("UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)) { layer = 5 };
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            go.AddComponent<AdaptiveMatch>();

            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                es.transform.SetParent(parent, false);
            }

            var root = go.AddComponent<UIRoot>();
            root.Canvas = canvas;
            root.screenLayer = UIKit.NewRect("Screens", go.transform).Stretch();
            root.modalLayer = UIKit.NewRect("Modals", go.transform).Stretch();
            root.overlayLayer = UIKit.NewRect("Overlay", go.transform).Stretch();
            root.overlayLayer.gameObject.AddComponent<SafeArea>();
            return root;
        }

        public T Show<T>(Action<T> setup = null) where T : UIScreen
        {
            CloseAllModals();
            var old = Current;
            var rt = UIKit.NewRect(typeof(T).Name, screenLayer);
            var screen = rt.gameObject.AddComponent<T>();
            setup?.Invoke(screen);
            screen.Init();
            Current = screen;
            var group = UIKit.Group(screen);
            group.alpha = 0f;
            StartCoroutine(Transition(old, group));
            return screen;
        }

        IEnumerator Transition(UIScreen old, CanvasGroup incoming)
        {
            if (old != null)
            {
                var og = UIKit.Group(old);
                og.blocksRaycasts = false;
                yield return Tween.All(this, Tween.Fade(og, 0f, 0.18f), Tween.Fade(incoming, 1f, 0.25f));
                Destroy(old.gameObject);
            }
            else
            {
                yield return Tween.Fade(incoming, 1f, 0.35f);
            }
        }

        public T Modal<T>(Action<T> setup = null) where T : UIModal
        {
            var rt = UIKit.NewRect(typeof(T).Name, modalLayer);
            rt.Stretch();
            var modal = rt.gameObject.AddComponent<T>();
            setup?.Invoke(modal);
            modal.InitModal();
            modals.Add(modal);
            var group = UIKit.Group(modal);
            group.alpha = 0f;
            StartCoroutine(Tween.Fade(group, 1f, 0.18f));
            if (modal.Panel != null)
            {
                modal.Panel.localScale = Vector3.one * 0.85f;
                StartCoroutine(Tween.Scale(modal.Panel, Vector3.one, 0.3f));
            }
            Casino.Audio.Sfx.Play(Casino.Audio.SfxId.Whoosh);
            return modal;
        }

        public T FindModal<T>() where T : UIModal
        {
            foreach (var m in modals)
                if (m is T t) return t;
            return null;
        }

        internal void CloseModal(UIModal modal)
        {
            if (!modals.Remove(modal)) return;
            StartCoroutine(FadeAndDestroy(modal));
        }

        IEnumerator FadeAndDestroy(UIModal modal)
        {
            var g = UIKit.Group(modal);
            g.blocksRaycasts = false;
            yield return Tween.Fade(g, 0f, 0.15f);
            if (modal != null) Destroy(modal.gameObject);
        }

        public void CloseAllModals()
        {
            for (int i = modals.Count - 1; i >= 0; i--)
                if (modals[i] != null) Destroy(modals[i].gameObject);
            modals.Clear();
        }

        /// <summary>Android back button / Escape: top modal first, then the screen.</summary>
        public bool Back()
        {
            if (modals.Count > 0) return modals[modals.Count - 1].OnBack();
            return Current != null && Current.OnBack();
        }

        public void Toast(string message, Color? accent = null, Icon icon = Icon.Star)
        {
            var bg = UIKit.Panel(overlayLayer, "Toast", new Color(0.08f, 0.04f, 0.14f, 0.95f), 30f);
            var rt = ((RectTransform)bg.transform).At(0.5f, 1f, 0, 120, 900, 90, 0.5f, 1f);
            UIKit.Outline(rt, accent ?? Palette.Gold, 30f);
            var ic = UIKit.Image(rt, "Icon", ProceduralArt.IconSprite(icon), accent ?? Palette.Gold);
            ((RectTransform)ic.transform).At(0f, 0.5f, 26, 0, 50, 50, 0f, 0.5f);
            var t = UIKit.Label(rt, message, 34, Color.white, TextAnchor.MiddleLeft, FontKind.Semi);
            ((RectTransform)t.transform).Stretch(96, 0, 20, 0);
            StartCoroutine(ToastRoutine(rt));
        }

        IEnumerator ToastRoutine(RectTransform rt)
        {
            yield return Tween.Move(rt, new Vector2(0, -130), 0.35f, Ease.OutBack);
            yield return Tween.Wait(2.2f);
            yield return Tween.Move(rt, new Vector2(0, 120), 0.25f, Ease.InQuad);
            if (rt != null) Destroy(rt.gameObject);
        }
    }

    /// <summary>Keeps a full 1920x1080 area visible on any aspect: wide phones get extra width, 4:3 tablets extra height.</summary>
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class AdaptiveMatch : MonoBehaviour
    {
        CanvasScaler scaler;
        Canvas canvas;
        float lastW, lastH;

        void Awake()
        {
            scaler = GetComponent<CanvasScaler>();
            canvas = GetComponent<Canvas>();
        }

        void Update()
        {
            // The canvas' own pixel size: the screen for overlay canvases, the target for camera canvases.
            Rect r = canvas.pixelRect;
            if (r.width <= 0 || (r.width == lastW && r.height == lastH)) return;
            lastW = r.width;
            lastH = r.height;
            scaler.matchWidthOrHeight = lastW * 1080f >= lastH * 1920f ? 1f : 0f;
        }
    }

    /// <summary>Fits a RectTransform to Screen.safeArea (notches, rounded corners, gesture bars).</summary>
    public sealed class SafeArea : MonoBehaviour
    {
        Rect last;
        RectTransform rt;

        void Awake() => rt = (RectTransform)transform;

        void Update()
        {
            // Desktop players report the safe area of the whole display, not the window, so only phones use it.
            Rect safe = Application.isMobilePlatform ? Screen.safeArea : new Rect(0, 0, Screen.width, Screen.height);
            if (safe == last || Screen.width <= 0 || Screen.height <= 0) return;
            last = safe;
            rt.anchorMin = new Vector2(Mathf.Clamp01(safe.xMin / Screen.width), Mathf.Clamp01(safe.yMin / Screen.height));
            rt.anchorMax = new Vector2(Mathf.Clamp01(safe.xMax / Screen.width), Mathf.Clamp01(safe.yMax / Screen.height));
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
