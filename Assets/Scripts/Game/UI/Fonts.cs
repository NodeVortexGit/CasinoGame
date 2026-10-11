using UnityEngine;

namespace Casino.UI
{
    public enum FontKind { Display, Body, Semi, Bold }

    /// <summary>Bebas Neue for big casino lettering, Poppins for everything else (both SIL OFL, in Resources/Fonts).</summary>
    public static class Fonts
    {
        static Font display, body, semi, bold, fallback;

        public static Font Get(FontKind kind)
        {
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
