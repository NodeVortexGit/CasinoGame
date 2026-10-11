using Casino.Art;
using UnityEngine;
using UnityEngine.UI;

namespace Casino.UI
{
    /// <summary>Frames per second in the bottom-left corner (Settings, Graphics: Show FPS), averaged over half a second.</summary>
    public sealed class FpsCounter : MonoBehaviour
    {
        static FpsCounter shown;
        Text text;
        int frames;
        float time;

        public static void Show(RectTransform overlay, bool on)
        {
            if (!on)
            {
                if (shown != null) Destroy(shown.gameObject);
                shown = null;
                return;
            }
            if (shown != null || overlay == null) return;
            var bg = UIKit.Panel(overlay, "Fps", new Color(0, 0, 0, 0.55f), 14f);
            var rt = ((RectTransform)bg.transform).At(0f, 0f, 16, 54, 150, 44, 0f, 0f);
            shown = bg.gameObject.AddComponent<FpsCounter>();
            shown.text = UIKit.Label(rt, "-- FPS", 26, Palette.GoldLight, TextAnchor.MiddleCenter, FontKind.Semi);
            ((RectTransform)shown.text.transform).Stretch();
        }

        void Update()
        {
            frames++;
            time += Time.unscaledDeltaTime;
            if (time < 0.5f) return;
            text.text = Mathf.RoundToInt(frames / time) + " FPS";
            frames = 0;
            time = 0f;
            transform.SetAsLastSibling();
        }
    }
}
