using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Casino.UI
{
    public enum FontKind { Display, Body, Semi, Bold }

    /// <summary>
    /// Bebas Neue for big casino lettering, Poppins for everything else (both SIL OFL, in Resources/Fonts). Languages
    /// those don't cover get Noto fonts made for them by Tools/build_locale.py (Resources/Lang/Fonts, SIL OFL).
    /// </summary>
    public static class Fonts
    {
        static Font display, body, semi, bold, fallback, languageNames;
        static readonly Dictionary<string, Font> made = new Dictionary<string, Font>();

        /// <summary>The casino's name is a logo: Bebas Neue in every language.</summary>
        public static Font Brand => display ??= Load("Fonts/BebasNeue-Regular");

        /// <summary>The font of the language list, which has every language's own name in it.</summary>
        public static Font LanguageNames => languageNames ??= Load("Lang/Fonts/languages");

        public static Font Get(FontKind kind)
        {
            string path = Loc.Current.Fonts[(int)kind];
            if (path != null)
            {
                if (!made.TryGetValue(path, out var f) || f == null) made[path] = f = Load(path);
                return f;
            }
            switch (kind)
            {
                case FontKind.Display: return display ??= Load("Fonts/BebasNeue-Regular");
                case FontKind.Semi: return semi ??= Load("Fonts/Poppins-SemiBold");
                case FontKind.Bold: return bold ??= Load("Fonts/Poppins-Bold");
                default: return body ??= Load("Fonts/Poppins-Regular");
            }
        }

        const string CommonChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,:;!?'\"-+x%$()/&";

        /// <summary>
        /// Draws the common letters and digits of the current language's fonts at the sizes the UI uses into their glyph
        /// atlases, a few sizes a frame, while the loading cover is up: otherwise the first screens do it mid-frame and hitch.
        /// </summary>
        public static IEnumerator Prewarm()
        {
            yield return null;   // the UI (and its canvas scale) exist from the next frame
            var language = Loc.Current;
            float scale = App.I != null && App.I.UI != null ? App.I.UI.Canvas.scaleFactor : 1f;
            var done = new HashSet<Font>();
            for (int k = 0; k < 4; k++)
            {
                var font = Get((FontKind)k);
                if (font == null || !done.Add(font)) continue;
                for (int size = 22; size <= 60; size++)
                {
                    font.RequestCharactersInTexture(CommonChars, Mathf.Max(1, Mathf.RoundToInt(size * scale)), FontStyle.Normal);
                    if (size % 6 == 0) yield return null;
                    if (Loc.Current != language) yield break;   // the language changed: its own prewarm has started
                }
            }
        }

        static Font Load(string path)
        {
            var f = Resources.Load<Font>(path);
            if (f != null) return f;
            return fallback ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
