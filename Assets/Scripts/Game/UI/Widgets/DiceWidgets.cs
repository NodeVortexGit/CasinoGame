using System.Collections;
using Casino.Art;
using UnityEngine;
using UnityEngine.UI;

namespace Casino.UI
{
    /// <summary>A die: an ivory rounded square with pips. Tumbles through random faces before it settles.</summary>
    public sealed class DieView : MonoBehaviour
    {
        static readonly Vector2[][] Pips =
        {
            new Vector2[0],
            new[] { Vector2.zero },
            new[] { new Vector2(-1, 1), new Vector2(1, -1) },
            new[] { new Vector2(-1, 1), Vector2.zero, new Vector2(1, -1) },
            new[] { new Vector2(-1, 1), new Vector2(1, 1), new Vector2(-1, -1), new Vector2(1, -1) },
            new[] { new Vector2(-1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(-1, -1), new Vector2(1, -1) },
            new[] { new Vector2(-1, 1), new Vector2(1, 1), new Vector2(-1, 0), new Vector2(1, 0), new Vector2(-1, -1), new Vector2(1, -1) },
        };

        readonly Image[] pips = new Image[6];
        float size;

        public int Value { get; private set; } = 1;
        public RectTransform Rect => (RectTransform)transform;

        public static DieView Create(Transform parent, float size, Color body, Color pip)
        {
            var rt = UIKit.NewRect("Die", parent);
            rt.sizeDelta = new Vector2(size, size);
            var d = rt.gameObject.AddComponent<DieView>();
            d.size = size;
            var shadow = UIKit.Image(rt, "Shadow", ProceduralArt.SoftShadow, new Color(0, 0, 0, 0.45f));
            shadow.type = Image.Type.Sliced;
            ((RectTransform)shadow.transform).Stretch(-size * 0.1f, -size * 0.16f, -size * 0.06f, -size * 0.02f);
            var face = UIKit.Panel(rt, "Face", body, size * 0.2f);
            ((RectTransform)face.transform).Stretch();
            for (int i = 0; i < 6; i++)
            {
                d.pips[i] = UIKit.Image(face.transform, "Pip", ProceduralArt.Circle, pip);
                ((RectTransform)d.pips[i].transform).Center(0, 0, size * 0.19f, size * 0.19f);
            }
            d.Set(1);
            return d;
        }

        public void Set(int value)
        {
            Value = Mathf.Clamp(value, 1, 6);
            var layout = Pips[Value];
            float step = size * 0.27f;
            for (int i = 0; i < 6; i++)
            {
                bool on = i < layout.Length;
                pips[i].enabled = on;
                if (on) ((RectTransform)pips[i].transform).anchoredPosition = layout[i] * step;
            }
        }

        /// <summary>Tumbles for <paramref name="seconds"/> along a path (layout positions), landing on <paramref name="value"/>.</summary>
        public IEnumerator Tumble(Vector2 from, Vector2 to, float seconds, int value, float spin = 720f)
        {
            float t = 0f, nextFace = 0f;
            float startAngle = Random.Range(0f, 360f);
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / seconds);
                float ease = 1f - (1f - k) * (1f - k);
                var p = Vector2.Lerp(from, to, ease);
                p.y += Mathf.Abs(Mathf.Sin(k * Mathf.PI * 3f)) * (1f - k) * size * 0.8f;   // a few bounces
                Rect.anchoredPosition = p;
                Rect.localEulerAngles = new Vector3(0, 0, startAngle + spin * ease);
                if (t >= nextFace && k < 0.9f)
                {
                    Set(Random.Range(1, 7));
                    nextFace = t + 0.07f;
                }
                yield return null;
            }
            Rect.anchoredPosition = to;
            Rect.localEulerAngles = new Vector3(0, 0, Random.Range(-14f, 14f));
            Set(value);
        }
    }
}
