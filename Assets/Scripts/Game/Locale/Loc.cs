using System;
using System.Collections.Generic;
using System.Globalization;
using Casino.Core;
using Casino.Core.Economy;
using UnityEngine;

namespace Casino
{
    /// <summary>One language the game is translated into (the list is generated: LanguageTable.cs).</summary>
    public sealed class LanguageInfo
    {
        public string Code, EnglishName;

        /// <summary>The language's own name, drawn with the language list's font (Fonts.LanguageNames).</summary>
        public string NativeName;

        public bool RightToLeft;

        /// <summary>Resources path of the font for each FontKind (Display, Body, Semi, Bold); null keeps the default font.</summary>
        public string[] Fonts;

        /// <summary>How numbers are written: the group and decimal separators and the group sizes (3 and 3, or 3 and 2).</summary>
        public string Group = ",", Decimal = ".";
        public int PrimaryGroup = 3, SecondaryGroup = 3;

        /// <summary>Prices: the currency sign before the number (as in $4.99) or after it (as in 4,99 €), and with a space.</summary>
        public bool CurrencyFirst = true, CurrencySpace;

        /// <summary>Device languages that pick this one (BCP 47 language or language-region, lower case).</summary>
        public string[] Matches;
    }

    /// <summary>
    /// The game in the player's language. Text in the code is English and wrapped where it is shown: Loc.T("PLAY")
    /// translates, Loc.F("{0} chips", n) translates and fills in, Loc.N("...") only marks text that is translated later,
    /// where it is shown (spot titles, product names; Tr.Mark in the engine-free core). Tools/build_locale.py collects
    /// the wrapped English into Tools/lang/en.txt, and builds the translations (Tools/lang/xx.txt) into Resources/Lang:
    /// keys.txt (the English, one phrase per line) and xx.txt (the translation of each line), plus the fonts.
    /// Scripts that need shaping (Arabic, Hebrew, the Indic scripts, Thai, Lao, Khmer, Myanmar, Sinhala) are shaped by
    /// HarfBuzz at build time into private-use glyphs of fonts made for them, so Unity's text draws them correctly;
    /// LocText puts right-to-left lines in reading order.
    /// </summary>
    public static class Loc
    {
        static Dictionary<string, string> table, plain;
        static string[] keys;

        public static LanguageInfo Current { get; private set; } = LanguageTable.English;
        public static bool RightToLeft => Current.RightToLeft;
        public static bool IsEnglish => Current == LanguageTable.English;

        /// <summary>The saved choice: "" follows the device's language.</summary>
        public static string Choice { get; private set; } = "";

        /// <summary>The language changed: screens rebuild themselves (App.Relocalize).</summary>
        public static event Action Changed;

        public static void Init(string choice)
        {
            Choice = choice ?? "";
            Apply(Resolve(Choice));
        }

        public static void Set(string choice)
        {
            Choice = choice ?? "";
            var lang = Resolve(Choice);
            if (lang == Current) return;
            Apply(lang);
            Changed?.Invoke();
        }

        /// <summary>Translates English shown to the player; English (or anything without a translation) comes back as is.</summary>
        public static string T(string english)
        {
            if (english == null || table == null) return english;
            return table.TryGetValue(english, out var t) ? t : english;
        }

        /// <summary>Translates a format string ({0}, {1}...) and fills it in. Falls back to the English if a translation is broken.</summary>
        public static string F(string english, params object[] args)
        {
            string format = T(english);
            if (Current.RightToLeft) KeepLeftToRight(args);
            try
            {
                return string.Format(CultureInfo.InvariantCulture, format, args);
            }
            catch (FormatException)
            {
                return string.Format(CultureInfo.InvariantCulture, english, args);
            }
        }

        static CultureInfo culture;

        /// <summary>Upper case by the language's rules (Turkish dotted İ, for one); scripts without case are unchanged.</summary>
        public static string Upper(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            // Georgian capitals (Mtavruli) are a display style, not the way labels are written.
            if (Current.Code == "ka") return s;
            if (culture == null)
            {
                try
                {
                    culture = CultureInfo.GetCultureInfo(Current.Code);
                }
                catch (Exception)
                {
                    culture = CultureInfo.InvariantCulture;
                }
            }
            return s.ToUpper(culture);
        }

        /// <summary>How the player's language writes prices.</summary>
        public static PriceStyle PriceStyle => new PriceStyle
        {
            SymbolFirst = Current.CurrencyFirst, Space = Current.CurrencySpace, Decimal = Current.Decimal, Group = Current.Group,
            PrimaryGroup = Current.PrimaryGroup, SecondaryGroup = Current.SecondaryGroup,
        };

        /// <summary>
        /// In right-to-left languages LocText reverses the order of the words on a line. A value of several left-to-right
        /// words (a controller's name) must keep its order, so its spaces become non-breaking; translated values (shaped
        /// into private-use characters) are right-to-left words already in reading order and are left alone.
        /// </summary>
        static void KeepLeftToRight(object[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (!(args[i] is string s) || s.IndexOf(' ') < 0) continue;
                bool rtl = false;
                foreach (char c in s)
                    if ((c >= '\uE000' && c <= '\uF8FF') || (c >= '\u0590' && c <= '\u08FF')) { rtl = true; break; }
                if (!rtl) args[i] = s.Replace(' ', '\u00a0');
            }
        }

        /// <summary>Marks English that is translated later, where it's shown (with <see cref="T"/>).</summary>
        public static string N(string english) => english;

        /// <summary>
        /// The translation as ordinary Unicode, for text the phone draws itself (notifications): for the languages
        /// shaped at build time, <see cref="T"/> gives glyph codes only the game's own fonts can show.
        /// </summary>
        public static string Plain(string english)
        {
            if (english == null) return null;
            if (plain == null) return T(english);
            return plain.TryGetValue(english, out var t) ? t : english;
        }

        public static string PlainF(string english, params object[] args)
        {
            try
            {
                return string.Format(CultureInfo.InvariantCulture, Plain(english), args);
            }
            catch (FormatException)
            {
                return string.Format(CultureInfo.InvariantCulture, english, args);
            }
        }

        /// <summary>A country's name in the player's language (ISO 3166-1 alpha-2 code).</summary>
        public static string Country(string iso2)
        {
            if (string.IsNullOrEmpty(iso2)) return "";
            string key = "@country:" + iso2.ToUpperInvariant();
            string name = T(key);
            return name != key ? name : iso2.ToUpperInvariant();
        }

        static readonly string[] EnglishWeekdays = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };

        /// <summary>A weekday's short name in the player's language (0 = Sunday, as DayOfWeek).</summary>
        public static string Weekday(int dayOfWeek)
        {
            string key = "@weekday:" + dayOfWeek;
            string name = T(key);
            return name != key ? name : EnglishWeekdays[dayOfWeek % 7];
        }

        /// <summary>A language's name in the player's language.</summary>
        public static string LanguageName(LanguageInfo lang)
        {
            string key = "@language:" + lang.Code;
            string name = T(key);
            return name != key ? name : lang.EnglishName;
        }

        public static LanguageInfo Find(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            foreach (var l in LanguageTable.All)
                if (string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase)) return l;
            return null;
        }

        /// <summary>The device's language, if the game has it, else English.</summary>
        public static LanguageInfo Detect()
        {
            foreach (var tag in DeviceLanguages())
            {
                var match = Match(tag);
                if (match != null) return match;
            }
            return LanguageTable.English;
        }

        /// <summary>The best language for a BCP 47 tag or POSIX locale ("pt-BR", "zh-Hant-TW", "sr_RS.UTF-8"), or null.</summary>
        public static LanguageInfo Match(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return null;
            int cut = tag.IndexOfAny(new[] { '.', '@' });
            if (cut >= 0) tag = tag.Substring(0, cut);
            var parts = tag.Replace('_', '-').ToLowerInvariant().Split('-');
            string lang = parts[0], region = "", script = "";
            for (int i = 1; i < parts.Length; i++)
            {
                if (parts[i].Length == 4) script = parts[i];
                else if (parts[i].Length == 2 || parts[i].Length == 3) region = parts[i];
            }
            switch (lang)
            {
                case "iw": lang = "he"; break;
                case "in": lang = "id"; break;
                case "no": case "nn": lang = "nb"; break;
                case "tl": lang = "fil"; break;
                case "zh": lang = script == "hant" || region == "tw" || region == "hk" || region == "mo" ? "zh-hant" : "zh-hans"; break;
            }
            string full = region.Length > 0 ? lang + "-" + region : lang;
            LanguageInfo byLanguage = null;
            foreach (var l in LanguageTable.All)
                foreach (var m in l.Matches)
                {
                    if (m == full) return l;
                    if (m == lang && byLanguage == null) byLanguage = l;
                }
            return byLanguage;
        }

        static IEnumerable<string> DeviceLanguages()
        {
            string android = Casino.Platform.AndroidBridge.LocaleTag();
            if (!string.IsNullOrEmpty(android)) yield return android;
            string steam = Casino.Platform.SteamBootstrap.Language();
            if (!string.IsNullOrEmpty(steam)) yield return steam;
            string env = Environment.GetEnvironmentVariable("LANGUAGE");   // Linux: "bg:en_GB:en"
            if (!string.IsNullOrEmpty(env))
                foreach (var part in env.Split(':'))
                    yield return part;
            string lang = Environment.GetEnvironmentVariable("LANG");
            if (!string.IsNullOrEmpty(lang)) yield return lang;
            string culture = null;
            try
            {
                culture = CultureInfo.CurrentUICulture.Name;
            }
            catch (Exception)
            {
                // no culture data: fall through
            }
            if (!string.IsNullOrEmpty(culture)) yield return culture;
            yield return SystemLanguageTag(Application.systemLanguage);
        }

        static string SystemLanguageTag(SystemLanguage l)
        {
            switch (l)
            {
                case SystemLanguage.ChineseTraditional: return "zh-Hant";
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified: return "zh-Hans";
                case SystemLanguage.Portuguese: return "pt";
                case SystemLanguage.Norwegian: return "nb";
                case SystemLanguage.SerboCroatian: return "sr";
                case SystemLanguage.Unknown: return null;
            }
            foreach (var c in CultureInfo.GetCultures(CultureTypes.NeutralCultures))
                if (c.EnglishName == l.ToString()) return c.Name;
            return null;
        }

        static LanguageInfo Resolve(string choice) => Find(choice) ?? Detect();

        static void Apply(LanguageInfo lang)
        {
            Current = lang;
            culture = null;
            ChipFormat.Group = lang.Group;
            ChipFormat.Decimal = lang.Decimal;
            ChipFormat.PrimaryGroup = lang.PrimaryGroup;
            ChipFormat.SecondaryGroup = lang.SecondaryGroup;
            table = plain = null;
            // English has a file too: it only names things that have no English in the code (countries, languages).
            var values = Lines("Lang/" + lang.Code);
            var english = Keys;
            if (values == null || english == null)
            {
                Debug.LogWarning("Language file missing: Lang/" + lang.Code);
                return;
            }
            table = Table(english, values);
            // Shaped languages also keep their text as plain Unicode, for the phone's own notifications.
            var plainValues = Lines("Lang/" + lang.Code + ".plain");
            if (plainValues != null) plain = Table(english, plainValues);
        }

        static Dictionary<string, string> Table(string[] english, string[] values)
        {
            var t = new Dictionary<string, string>(english.Length);
            for (int i = 0; i < english.Length && i < values.Length; i++)
                if (values[i].Length > 0) t[english[i]] = values[i];
            return t;
        }

        static string[] Keys => keys ??= Lines("Lang/keys");

        static string[] Lines(string path)
        {
            var asset = Resources.Load<TextAsset>(path);
            if (asset == null) return null;
            string text = asset.text;
            Resources.UnloadAsset(asset);
            var lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++) lines[i] = Unescape(lines[i]);
            return lines;
        }

        static string Unescape(string s)
        {
            if (s.IndexOf('\\') < 0) return s.TrimEnd('\r');
            var sb = new System.Text.StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length)
                {
                    char n = s[++i];
                    sb.Append(n == 'n' ? '\n' : n == 't' ? '\t' : n);
                }
                else if (c != '\r') sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
