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

        static Font Load(string path)
        {
            var f = Resources.Load<Font>(path);
            if (f != null) return f;
            return fallback ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
