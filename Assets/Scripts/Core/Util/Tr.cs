using System;
using System.Globalization;

namespace Casino.Core
{
    /// <summary>
    /// English text that players see. Tools/build_locale.py collects the text passed to these (and every string in a
    /// file marked "// @translate-all-strings"). The game layer sets <see cref="Translate"/> to its language lookup;
    /// until then (and in the tests) text stays English.
    /// </summary>
    public static class Tr
    {
        public static Func<string, string> Translate = s => s;

        /// <summary>Collected, but translated later where it's shown (this layer doesn't know the player's language).</summary>
        public static string Mark(string english) => english;

        /// <summary>Translated now.</summary>
        public static string T(string english) => Translate(english);

        /// <summary>Translated now, with {0}, {1}... filled in. Falls back to the English if a translation is broken.</summary>
        public static string F(string english, params object[] args)
        {
            try
            {
                return string.Format(CultureInfo.InvariantCulture, Translate(english), args);
            }
            catch (FormatException)
            {
                return string.Format(CultureInfo.InvariantCulture, english, args);
            }
        }
    }
}
