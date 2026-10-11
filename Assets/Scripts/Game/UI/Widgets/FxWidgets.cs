using System;
using System.Collections.Generic;
using Casino.Art;
using UnityEngine;
using UnityEngine.UI;

namespace Casino.UI
{
    /// <summary>Burst of falling confetti and coins. Lives on its own sub-canvas so it doesn't dirty the main UI.</summary>
    public sealed class Confetti : MonoBehaviour
    {
        struct Piece
        {
            public RectTransform Rt;
            public Vector2 Vel;
            public float Spin;
        }

        readonly List<Piece> pieces = new List<Piece>();
        float life;

        public static void Burst(Transform parent, Vector2 origin, int count = 60, bool coins = false)
        {
            var rt = UIKit.NewRect("Confetti", parent).Stretch();
            var canvas = rt.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 50;
            var c = rt.gameObject.AddComponent<Confetti>();
            Color[] colors = { Palette.Gold, Palette.Pink, Palette.Cyan, Palette.Green, Color.white, Palette.Orange };
            for (int i = 0; i < count; i++)
            {
                bool coin = coins && i % 2 == 0;
                var img = UIKit.Image(rt, "Bit", coin ? ProceduralArt.IconSprite(Icon.Coin) : null,
                    coin ? Color.white : colors[i % colors.Length]);
                var prt = ((RectTransform)img.transform).Center(origin.x, origin.y, coin ? 44 : 14, coin ? 44 : 24);
                float ang = UnityEngine.Random.Range(30f, 150f) * Mathf.Deg2Rad;
                float speed = UnityEngine.Random.Range(600f, 1400f);
                c.pieces.Add(new Piece { Rt = prt, Vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * speed, Spin = UnityEngine.Random.Range(-720f, 720f) });
            }
        }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            life += dt;
            for (int i = 0; i < pieces.Count; i++)
            {
                var p = pieces[i];
                p.Vel += new Vector2(0, -1900f) * dt;
                p.Vel *= 1f - 0.9f * dt;
                p.Rt.anchoredPosition += p.Vel * dt;
                p.Rt.Rotate(0, 0, p.Spin * dt);
                pieces[i] = p;
            }
            if (life > 2.6f) Destroy(gameObject);
        }
    }

    /// <summary>Ambient twinkling sparkles for backgrounds.</summary>
    public sealed class Twinkles : MonoBehaviour
    {
        Image[] stars;
        float[] phases, speeds;

        public static Twinkles Create(RectTransform parent, int count, Rect area)
        {
            var rt = UIKit.NewRect("Twinkles", parent).Stretch();
            var canvas = rt.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = false;
            var t = rt.gameObject.AddComponent<Twinkles>();
            t.stars = new Image[count];
            t.phases = new float[count];
            t.speeds = new float[count];
            for (int i = 0; i < count; i++)
            {
                var img = UIKit.Image(rt, "Sparkle", ProceduralArt.Sparkle, Color.white);
                float s = UnityEngine.Random.Range(26f, 64f);
                ((RectTransform)img.transform).Center(UnityEngine.Random.Range(area.xMin, area.xMax), UnityEngine.Random.Range(area.yMin, area.yMax), s, s);
                t.stars[i] = img;
                t.phases[i] = UnityEngine.Random.value * 10f;
                t.speeds[i] = UnityEngine.Random.Range(0.6f, 1.6f);
            }
            return t;
        }

        void Update()
        {
            float time = Time.unscaledTime;
            for (int i = 0; i < stars.Length; i++)
            {
                float a = Mathf.Max(0f, Mathf.Sin(time * speeds[i] + phases[i]));
                stars[i].color = new Color(1f, 0.95f, 0.8f, a * a * 0.9f);
                stars[i].transform.localScale = Vector3.one * (0.5f + a * 0.5f);
            }
        }
    }

    /// <summary>Chasing marquee bulbs around a rectangle, like an old Vegas sign.</summary>
    public sealed class Marquee : MonoBehaviour
    {
        Image[] bulbs;

        public static Marquee Create(RectTransform parent, Vector2 size, float spacing = 46f)
        {
            var rt = UIKit.NewRect("Marquee", parent);
            rt.Center(0, 0, size.x, size.y);
            var canvas = rt.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = false;
            var m = rt.gameObject.AddComponent<Marquee>();
            var points = new List<Vector2>();
            float w = size.x / 2f, h = size.y / 2f;
            for (float x = -w; x < w; x += spacing) points.Add(new Vector2(x, h));
            for (float y = h; y > -h; y -= spacing) points.Add(new Vector2(w, y));
            for (float x = w; x > -w; x -= spacing) points.Add(new Vector2(x, -h));
            for (float y = -h; y < h; y += spacing) points.Add(new Vector2(-w, y));
            m.bulbs = new Image[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                var img = UIKit.Image(rt, "Bulb", ProceduralArt.Bulb, Palette.GoldLight);
                ((RectTransform)img.transform).Center(points[i].x, points[i].y, 30, 30);
                m.bulbs[i] = img;
            }
            return m;
        }

        void Update()
        {
            int lit = Mathf.FloorToInt(Time.unscaledTime * 8f);
            for (int i = 0; i < bulbs.Length; i++)
            {
                bool on = (i + lit) % 3 == 0;
                bulbs[i].color = on ? Palette.GoldLight : Palette.WithAlpha(Palette.GoldDark, 0.55f);
            }
        }
    }

    /// <summary>Slowly rotating light rays behind something important.</summary>
    public sealed class Spinner : MonoBehaviour
    {
        public float DegreesPerSecond = 8f;
        void Awake() => UIKit.OwnCanvas(gameObject, false);
        void Update() => transform.Rotate(0, 0, DegreesPerSecond * Time.unscaledDeltaTime);
    }

    /// <summary>Gentle scale pulse (call-to-action buttons, banners).</summary>
    public sealed class Pulse : MonoBehaviour
    {
        public float Amount = 0.04f, Speed = 3f;
        void Awake() => UIKit.OwnCanvas(gameObject, true);
        void Update()
        {
            float s = 1f + Mathf.Sin(Time.unscaledTime * Speed) * Amount;
            transform.localScale = new Vector3(s, s, 1f);
        }
    }

    /// <summary>On/off switch.</summary>
    public sealed class Switch : MonoBehaviour
    {
        Image track;
        RectTransform knob;
        Action<bool> changed;
        public bool On { get; private set; }

        public static Switch Create(Transform parent, bool on, Action<bool> onChanged)
        {
            var bg = UIKit.Panel(parent, "Switch", Color.gray, 30f, true);
            var rt = (RectTransform)bg.transform;
            rt.sizeDelta = new Vector2(120, 60);
            var s = bg.gameObject.AddComponent<Switch>();
            s.track = bg;
            var k = UIKit.Image(rt, "Knob", ProceduralArt.Circle, Color.white);
            s.knob = ((RectTransform)k.transform).At(0f, 0.5f, 0, 0, 50, 50);
            s.On = on;
            s.Apply(false);
            s.changed = onChanged;
            bg.gameObject.AddComponent<Button>().onClick.AddListener(() =>
            {
                s.On = !s.On;
                s.Apply(true);
                Casino.Audio.Sfx.Play(Casino.Audio.SfxId.Click);
                s.changed?.Invoke(s.On);
            });
            return s;
        }

        void Apply(bool animate)
        {
            track.color = On ? Palette.Green : Palette.Hex("4A3D5C");
            var target = new Vector2(On ? 90 : 30, 0);
            if (animate) StartCoroutine(Tween.Move(knob, target, 0.15f, Ease.OutQuad));
            else knob.anchoredPosition = target;
        }
    }

    /// <summary>0..1 slider built from code.</summary>
    public static class SliderFactory
    {
        public static Slider Create(Transform parent, float value, Action<float> changed, float width = 420f)
        {
            var rt = UIKit.NewRect("Slider", parent);
            rt.sizeDelta = new Vector2(width, 50);
            var slider = rt.gameObject.AddComponent<Slider>();
            var bg = UIKit.Panel(rt, "Track", Palette.Hex("4A3D5C"), 10f);
            ((RectTransform)bg.transform).Stretch(0, 16, 0, 16);
            var fillArea = UIKit.NewRect("FillArea", rt).Stretch(0, 16, 0, 16);
            var fill = UIKit.Panel(fillArea, "Fill", Palette.Gold, 10f);
            var frt = (RectTransform)fill.transform;
            frt.sizeDelta = Vector2.zero;
            var handleArea = UIKit.NewRect("HandleArea", rt).Stretch(22, 0, 22, 0);
            var handle = UIKit.Image(handleArea, "Handle", ProceduralArt.Circle, Color.white, true);
            var hrt = (RectTransform)handle.transform;
            hrt.sizeDelta = new Vector2(44, 0);
            slider.fillRect = frt;
            slider.handleRect = hrt;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.value = value;
            slider.onValueChanged.AddListener(v => changed(v));
            return slider;
        }
    }

    /// <summary>Row of mutually exclusive options.</summary>
    public static class Segmented
    {
        public static RectTransform Create(Transform parent, string[] options, int selected, Action<int> changed, float width = 540f)
        {
            var rt = UIKit.NewRect("Segmented", parent);
            rt.sizeDelta = new Vector2(width, 64);
            var buttons = new UIButton[options.Length];
            float w = width / options.Length;
            for (int i = 0; i < options.Length; i++)
            {
                buttons[i] = UIKit.Button(rt, options[i], i == selected ? ButtonStyle.Gold : ButtonStyle.Dark, null, 30, null, FontKind.Semi, 20f);
                buttons[i].Rect.At(0f, 0.5f, i * w + 4, 0, w - 8, 60, 0f, 0.5f);
            }
            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                buttons[i].SetOnClick(() =>
                {
                    for (int j = 0; j < buttons.Length; j++) buttons[j].SetStyle(j == index ? ButtonStyle.Gold : ButtonStyle.Dark);
                    changed(index);
                });
            }
            return rt;
        }
    }
}
