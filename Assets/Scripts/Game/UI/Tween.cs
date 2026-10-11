using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Casino.UI
{
    public enum Ease { Linear, OutQuad, InQuad, InOutQuad, OutCubic, InCubic, OutBack, OutElastic, InBack }

    /// <summary>Tiny coroutine tween library: start the returned IEnumerator on any MonoBehaviour.</summary>
    public static class Tween
    {
        public static float Evaluate(Ease ease, float t)
        {
            t = Mathf.Clamp01(t);
            switch (ease)
            {
                case Ease.OutQuad: return 1f - (1f - t) * (1f - t);
                case Ease.InQuad: return t * t;
                case Ease.InOutQuad: return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
                case Ease.OutCubic: return 1f - Mathf.Pow(1f - t, 3f);
                case Ease.InCubic: return t * t * t;
                case Ease.OutBack:
                {
                    const float c1 = 1.70158f, c3 = c1 + 1f;
                    return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
                }
                case Ease.InBack:
                {
                    const float c1 = 1.70158f, c3 = c1 + 1f;
                    return c3 * t * t * t - c1 * t * t;
                }
                case Ease.OutElastic:
                {
                    if (t <= 0f || t >= 1f) return t;
                    const float c4 = 2f * Mathf.PI / 3f;
                    return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
                }
                default: return t;
            }
        }

        public static IEnumerator Run(float duration, Ease ease, Action<float> step)
        {
            if (duration <= 0f)
            {
                step(1f);
                yield break;
            }
            float t = 0f;
            while (t < duration)
            {
                step(Evaluate(ease, t / duration));
                yield return null;
                t += Time.unscaledDeltaTime;
            }
            step(1f);
        }

        public static IEnumerator Move(RectTransform rt, Vector2 to, float duration, Ease ease = Ease.OutCubic)
        {
            if (rt == null) yield break;
            Vector2 from = rt.anchoredPosition;
            yield return Run(duration, ease, k => { if (rt != null) rt.anchoredPosition = Vector2.LerpUnclamped(from, to, k); });
        }

        public static IEnumerator Scale(Transform tr, Vector3 to, float duration, Ease ease = Ease.OutBack)
        {
            if (tr == null) yield break;
            Vector3 from = tr.localScale;
            yield return Run(duration, ease, k => { if (tr != null) tr.localScale = Vector3.LerpUnclamped(from, to, k); });
        }

        public static IEnumerator Rotate(Transform tr, float toZ, float duration, Ease ease = Ease.OutCubic)
        {
            if (tr == null) yield break;
            float from = tr.localEulerAngles.z;
            if (from > 180f) from -= 360f;
            yield return Run(duration, ease, k => { if (tr != null) tr.localEulerAngles = new Vector3(0, 0, Mathf.LerpUnclamped(from, toZ, k)); });
        }

        public static IEnumerator Fade(CanvasGroup group, float to, float duration, Ease ease = Ease.Linear)
        {
            if (group == null) yield break;
            float from = group.alpha;
            yield return Run(duration, ease, k => { if (group != null) group.alpha = Mathf.LerpUnclamped(from, to, k); });
        }

        public static IEnumerator Color(Graphic g, Color to, float duration, Ease ease = Ease.Linear)
        {
            if (g == null) yield break;
            Color from = g.color;
            yield return Run(duration, ease, k => { if (g != null) g.color = UnityEngine.Color.LerpUnclamped(from, to, k); });
        }

        public static IEnumerator Punch(Transform tr, float amount = 0.15f, float duration = 0.3f)
        {
            if (tr == null) yield break;
            Vector3 baseScale = tr.localScale;
            yield return Run(duration, Ease.Linear, k =>
            {
                if (tr != null) tr.localScale = baseScale * (1f + Mathf.Sin(k * Mathf.PI) * amount);
            });
        }

        public static IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                yield return null;
                t += Time.unscaledDeltaTime;
            }
        }

        /// <summary>Runs several tweens at once on <paramref name="host"/> and finishes when all of them are done.</summary>
        public static IEnumerator All(MonoBehaviour host, params IEnumerator[] routines)
        {
            int running = routines.Length;
            foreach (var r in routines) host.StartCoroutine(Track(r, () => running--));
            while (running > 0) yield return null;
        }

        static IEnumerator Track(IEnumerator routine, Action done)
        {
            yield return routine;
            done();
        }
    }
}
