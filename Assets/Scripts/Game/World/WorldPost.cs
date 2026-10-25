using UnityEngine;

namespace Casino.World
{
    /// <summary>
    /// The world camera's finishing pass at the high graphics settings (shader Hidden/Casino/Post): the casino as a film
    /// camera would shoot it. Bloom, so lights bleed softly into the air around them as through a real lens, filmic tone
    /// mapping so bright neon rolls off smoothly instead of clipping flat, a film grade, grain and a vignette; at Ultra
    /// also depth of field that focuses on whatever is in the middle of the view, the long horizontal streaks of an
    /// anamorphic lens and faint colour fringes at the edges. Disabled at the lower settings, where it costs nothing.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class WorldPost : MonoBehaviour
    {
        const int Prefilter = 0, Down = 1, Up = 2, Final = 3, StreakDown = 4, StreakUp = 5, FocusPass = 6, DofPrefilter = 7,
            Bokeh = 8, Tent = 9;
        const int MaxLevels = 8, StreakLevels = 6;

        static readonly int ThresholdId = Shader.PropertyToID("_Threshold"), KneeId = Shader.PropertyToID("_Knee"),
            BloomId = Shader.PropertyToID("_Bloom"), StrengthId = Shader.PropertyToID("_BloomStrength"),
            ExposureId = Shader.PropertyToID("_Exposure"), VignetteId = Shader.PropertyToID("_Vignette"),
            StreakId = Shader.PropertyToID("_Streak"), StreakStrengthId = Shader.PropertyToID("_StreakStrength"),
            StreakTintId = Shader.PropertyToID("_StreakTint"), GradeId = Shader.PropertyToID("_Grade"),
            GrainId = Shader.PropertyToID("_Grain"), GrainSeedId = Shader.PropertyToID("_GrainSeed"),
            AberrationId = Shader.PropertyToID("_Aberration"), DofTexId = Shader.PropertyToID("_DofTex"),
            DofParamsId = Shader.PropertyToID("_DofParams"), FocusTexId = Shader.PropertyToID("_FocusTex"),
            FocusBlendId = Shader.PropertyToID("_FocusBlend"), FocusTargetId = Shader.PropertyToID("_FocusTarget");

        /// <summary>How many halvings the bloom blurs over: more spreads it wider.</summary>
        public int BloomLevels = 5;

        /// <summary>Start the bloom at half resolution instead of a quarter: finer, steadier glows, more work.</summary>
        public bool FineBloom;

        public float BloomStrength = 0.45f, Threshold = 1.15f, Knee = 0.3f, Exposure = 1f, Vignette = 0.22f;

        /// <summary>How much of the film grade: cool shadows, warm highlights, a gentle S-curve (0..1).</summary>
        public float Grade;

        /// <summary>Film grain, moving every frame (0 = none).</summary>
        public float Grain;

        /// <summary>Horizontal streaks from the brightest lights, as an anamorphic cinema lens draws them (0 = none).</summary>
        public float Streaks;

        /// <summary>Red and blue fringes towards the edges of the frame (0 = none).</summary>
        public float Aberration;

        /// <summary>
        /// Depth of field: the camera focuses on whatever is in the middle of the view and pulls focus smoothly when that
        /// changes. Needs the camera's depth texture (<see cref="CasinoWorld.ApplyGraphics"/> turns it on).
        /// </summary>
        public bool DepthOfField;

        /// <summary>How quickly things blur away from the focus, and the widest blur as a fraction of the screen height.</summary>
        public float Aperture = 1.6f, MaxBlur = 0.014f;

        /// <summary>Things nearer than the focus blur this much compared with things behind it (so what's at your elbow
        /// doesn't smear while you look across the room; 0 while seated, so the cards in front of the dealer stay sharp).</summary>
        public float NearBlur = 0.15f;

        /// <summary>
        /// A face to focus on: x, y = where it is in the view (0..1), z = how far away (m), w = 1 if there is one. It's used
        /// only while the face can really be seen (checked against the depth), else the autofocus takes the middle of the view.
        /// </summary>
        [System.NonSerialized] public Vector4 FocusTarget;

        /// <summary>How fast the focus pulls to a new distance (per second).</summary>
        public float FocusSpeed = 2.5f;

        static readonly Color StreakTint = new Color(0.7f, 0.85f, 1.2f);

        readonly RenderTexture[] chain = new RenderTexture[MaxLevels];
        readonly RenderTexture[] streaks = new RenderTexture[StreakLevels];
        readonly RenderTexture[] focus = new RenderTexture[2];
        int focusCurrent;
        bool snapFocus = true;
        Material material;
        bool unsupported;

        /// <summary>Focus at once on what's in view next frame, instead of pulling focus to it (after a teleport).</summary>
        public void SnapFocus() => snapFocus = true;

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (material == null && !unsupported)
            {
                var shader = Resources.Load<Shader>("Shaders/CasinoPost");
                if (shader != null && shader.isSupported) material = new Material(shader) { hideFlags = HideFlags.DontSave };
                else unsupported = true;
            }
            if (material == null)
            {
                Graphics.Blit(source, destination);
                return;
            }

            material.SetFloat(ThresholdId, Threshold);
            material.SetFloat(KneeId, Knee);
            material.SetFloat(ExposureId, Exposure);
            material.SetFloat(VignetteId, Vignette);
            material.SetFloat(GradeId, Grade);
            material.SetFloat(GrainId, Grain);
            material.SetFloat(GrainSeedId, Time.frameCount % 1009 * 0.618034f % 1f);
            material.SetFloat(AberrationId, Aberration);

            var dof = DepthOfField ? RenderDepthOfField(source) : null;
            SetKeyword("DEPTH_OF_FIELD", dof != null);

            int shift = FineBloom ? 1 : 2;
            int w = source.width >> shift, h = source.height >> shift, levels = 0;
            var last = source;
            for (; levels < Mathf.Min(BloomLevels, MaxLevels) && w >= 4 && h >= 4; levels++)
            {
                chain[levels] = RenderTexture.GetTemporary(w, h, 0, source.format);
                chain[levels].filterMode = FilterMode.Bilinear;
                Graphics.Blit(last, chain[levels], material, levels == 0 ? Prefilter : Down);
                last = chain[levels];
                w >>= 1;
                h >>= 1;
            }
            // The streaks start from the same bright parts as the bloom, before the bloom's blur is added onto them.
            int streakLevels = Streaks > 0f && levels > 0 ? RenderStreaks(chain[0]) : 0;
            SetKeyword("LENS", streakLevels > 0 || Aberration > 0f);
            // Back up the pyramid, adding each blurrier level onto the sharper one above it.
            for (int i = levels - 1; i > 0; i--) Graphics.Blit(chain[i], chain[i - 1], material, Up);

            material.SetTexture(BloomId, levels > 0 ? (Texture)chain[0] : Texture2D.blackTexture);
            material.SetFloat(StrengthId, levels > 0 ? BloomStrength / levels : 0f);
            material.SetTexture(StreakId, streakLevels > 0 ? (Texture)streaks[0] : Texture2D.blackTexture);
            material.SetFloat(StreakStrengthId, streakLevels > 0 ? Streaks / streakLevels : 0f);
            material.SetColor(StreakTintId, StreakTint);
            Graphics.Blit(source, destination, material, Final);

            for (int i = 0; i < levels; i++)
            {
                RenderTexture.ReleaseTemporary(chain[i]);
                chain[i] = null;
            }
            for (int i = 0; i < streakLevels; i++)
            {
                RenderTexture.ReleaseTemporary(streaks[i]);
                streaks[i] = null;
            }
            if (dof != null) RenderTexture.ReleaseTemporary(dof);
        }

        /// <summary>A pyramid that halves only the width, from <paramref name="bright"/>; returns its depth.</summary>
        int RenderStreaks(RenderTexture bright)
        {
            int w = bright.width >> 1, h = bright.height, n = 0;
            var last = bright;
            for (; n < StreakLevels && w >= 4; n++)
            {
                streaks[n] = RenderTexture.GetTemporary(w, h, 0, bright.format);
                streaks[n].filterMode = FilterMode.Bilinear;
                Graphics.Blit(last, streaks[n], material, StreakDown);
                last = streaks[n];
                w >>= 1;
            }
            for (int i = n - 1; i > 0; i--) Graphics.Blit(streaks[i], streaks[i - 1], material, StreakUp);
            return n;
        }

        /// <summary>Moves the autofocus on, then blurs the frame at half resolution; the caller releases the result.</summary>
        RenderTexture RenderDepthOfField(RenderTexture source)
        {
            for (int i = 0; i < focus.Length; i++)
            {
                if (focus[i] != null && focus[i].IsCreated()) continue;
                if (focus[i] == null)
                    focus[i] = new RenderTexture(1, 1, 0, RenderTextureFormat.ARGBHalf) { name = "Focus", hideFlags = HideFlags.DontSave };
                focus[i].Create();
                snapFocus = true;
            }
            material.SetFloat(FocusBlendId, snapFocus ? 1f : 1f - Mathf.Exp(-Time.unscaledDeltaTime * FocusSpeed));
            material.SetVector(FocusTargetId, FocusTarget);
            snapFocus = false;
            Graphics.Blit(focus[focusCurrent], focus[1 - focusCurrent], material, FocusPass);
            focusCurrent = 1 - focusCurrent;
            material.SetTexture(FocusTexId, focus[focusCurrent]);
            material.SetVector(DofParamsId, new Vector4(Aperture, MaxBlur, NearBlur, 0f));

            int w = Mathf.Max(source.width / 2, 1), h = Mathf.Max(source.height / 2, 1);
            var half = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGBHalf);
            var bokeh = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGBHalf);
            half.filterMode = bokeh.filterMode = FilterMode.Bilinear;
            Graphics.Blit(source, half, material, DofPrefilter);
            Graphics.Blit(half, bokeh, material, Bokeh);
            Graphics.Blit(bokeh, half, material, Tent);
            RenderTexture.ReleaseTemporary(bokeh);
            material.SetTexture(DofTexId, half);
            return half;
        }

        void SetKeyword(string keyword, bool on)
        {
            if (on) material.EnableKeyword(keyword);
            else material.DisableKeyword(keyword);
        }

        void OnDestroy()
        {
            if (material != null) Destroy(material);
            foreach (var t in focus)
                if (t != null)
                {
                    t.Release();
                    Destroy(t);
                }
        }
    }
}
