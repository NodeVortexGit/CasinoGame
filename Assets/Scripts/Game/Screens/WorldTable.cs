using Casino.Audio;
using Casino.UI;
using Casino.World;
using UnityEngine;
using UnityEngine.UI;

namespace Casino.Screens
{
    /// <summary>
    /// Plumbing for a game played inside the 3D casino: the player sits down (the camera glides to the seat), the
    /// game's pieces (cards, reels, the betting board) live on a layer in the room itself, the member of staff running
    /// the game is the real 3D person, and their lines float over their head. Buttons stay on the screen (HUD).
    /// </summary>
    public sealed class WorldTable
    {
        public CasinoWorld World { get; private set; }
        public StaffFigure Staff { get; private set; }

        /// <summary>The game layer in the room (layout units, like a 1920 x 1080 screen; +y points away from you).</summary>
        public RectTransform Layer { get; private set; }

        public Vector3 Eye { get; private set; }

        RectTransform hud, bubbleRect;
        int seatId;
        SpeechBubble bubble;
        CanvasGroup layerGroup;
        float speakUntil, fadeIn;

        /// <summary>
        /// A world-space canvas: <paramref name="size"/> layout units, <paramref name="scale"/> metres per unit, drawn by
        /// the casino camera (layer 0); <paramref name="interactive"/> lets taps reach it.
        /// </summary>
        public static RectTransform CreateLayer(string name, Transform parent, Vector3 position, Quaternion rotation, float scale,
            Vector2 size, Camera eventCamera, bool interactive)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = 0 };
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = eventCamera;
            if (interactive) go.AddComponent<GraphicRaycaster>();
            rt.sizeDelta = size;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.SetPositionAndRotation(position, rotation);
            rt.localScale = Vector3.one * scale;
            return rt;
        }

        /// <summary>Sits down and builds the game layer. Returns null when the casino isn't there to sit in.</summary>
        public static WorldTable Sit(RectTransform hud, Vector3 eye, Vector3 lookAt, float verticalFov, float minHorizontalFov,
            Vector2 standAt, float standYaw, Vector3 layerPosition, Quaternion layerRotation, float layerScale, StaffFigure staff,
            bool interactive = false)
        {
            var world = CasinoWorld.Instance;
            if (world == null || !world.Ready) return null;
            world.SetVisible(true);
            int id = world.SitDown(eye, lookAt, world.SeatFov(verticalFov, minHorizontalFov), standAt, standYaw);
            var t = new WorldTable { World = world, Staff = staff, hud = hud, Eye = eye, seatId = id };
            t.Layer = CreateLayer("GameLayer", world.transform, layerPosition, layerRotation, layerScale, new Vector2(1920, 1080),
                world.Camera, interactive);
            // The layer fades in once the camera has settled into the seat.
            t.layerGroup = t.Layer.gameObject.AddComponent<CanvasGroup>();
            t.layerGroup.alpha = 0f;
            t.fadeIn = 0.45f;
            t.bubble = SpeechBubble.Create(hud, 520, 120);
            t.bubbleRect = (RectTransform)t.bubble.transform;
            t.bubbleRect.anchorMin = t.bubbleRect.anchorMax = new Vector2(0.5f, 0.5f);
            t.bubbleRect.pivot = new Vector2(40f / 520f, 0f);
            return t;
        }

        /// <summary>The member of staff says a line (their mouth moves, the bubble floats over their head).</summary>
        public void Say(string line, float hold)
        {
            if (Staff != null) Staff.Talk(Mathf.Clamp(line.Length * 0.045f, 1.2f, 3.5f));
            speakUntil = Time.unscaledTime + hold + line.Length * 0.022f + 0.4f;
            bubble.Say(line, hold, null, Staff == null ? VoicePitch.None : Staff.Female ? VoicePitch.High : VoicePitch.Low);
            PlaceBubble();
        }

        /// <summary>Call every frame from the screen's Update.</summary>
        public void Tick()
        {
            if (fadeIn > 0f)
            {
                fadeIn -= Time.unscaledDeltaTime;
                if (fadeIn <= 0f) World.StartCoroutine(Tween.Fade(layerGroup, 1f, 0.3f));
            }
            if (Time.unscaledTime < speakUntil) PlaceBubble();
        }

        void PlaceBubble()
        {
            if (Staff == null) return;
            Vector3 screen = World.Camera.WorldToScreenPoint(Staff.HeadPoint + Vector3.up * 0.1f);
            if (screen.z > 0.2f && ScreenToHud(screen, out var local))
            {
                var half = hud.rect.size * 0.5f;
                local.x = Mathf.Clamp(local.x + 60f, -half.x + 60f, half.x - 540f);
                local.y = Mathf.Clamp(local.y, -half.y + 220f, half.y - TopBar.Height - bubbleRect.sizeDelta.y - 20f);
                bubbleRect.anchoredPosition = local;
            }
        }

        bool ScreenToHud(Vector2 screen, out Vector2 local)
        {
            var canvas = App.I.UI.Canvas;
            var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(hud, screen, cam, out local);
        }

        /// <summary>A point in the room as a position inside <paramref name="rect"/> on the HUD (for stamps, banners).</summary>
        public Vector2 ToHudLocal(Vector3 world, RectTransform rect)
        {
            Vector2 screen = World.Camera.WorldToScreenPoint(world);
            var canvas = App.I.UI.Canvas;
            var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, cam, out var local);
            return local;
        }

        /// <summary>A point in the room as a world position on the HUD canvas (for things flying between the two).</summary>
        public Vector3 ToHudWorld(Vector3 world)
        {
            Vector2 screen = World.Camera.WorldToScreenPoint(world);
            var canvas = App.I.UI.Canvas;
            var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransformUtility.ScreenPointToWorldPointInRectangle(hud, screen, cam, out var p);
            return p;
        }

        /// <summary>
        /// Where a real object in the room appears on the game layer, seen from the seat: e.g. the 3D card shoe, so cards
        /// slide out of it. Layout units.
        /// </summary>
        public Vector2 OnLayer(Vector3 world)
        {
            var plane = new Plane(-Layer.forward, Layer.position);
            var ray = new Ray(Eye, world - Eye);
            if (!plane.Raycast(ray, out float d)) return Vector2.zero;
            return Layer.InverseTransformPoint(ray.GetPoint(d));
        }

        /// <summary>Leaves the game: the layer goes, the camera glides back to standing.</summary>
        public void Dispose()
        {
            if (Layer != null) Object.Destroy(Layer.gameObject);
            if (World != null) World.StandUp(seatId);
        }
    }
}
