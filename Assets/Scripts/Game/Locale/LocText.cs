using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Casino.UI
{
    /// <summary>
    /// The game's text (every label is one, made by UIKit.Label). In English it is plain Text. In other languages it
    /// shrinks (to 60% at most) where a translation is longer than the English the layout was made for, and in
    /// right-to-left languages it breaks lines itself and lays each one out from the right: the translations keep their
    /// words in reading order, each right-to-left word already shaped into display order (Tools/build_locale.py), so
    /// a line is its words reversed, with any numbers or Latin words left as they are.
    /// </summary>
    public sealed class LocText : Text
    {
        static readonly HashSet<LocText> pending = new HashSet<LocText>();
        static readonly TextGenerator measure = new TextGenerator();
        static readonly List<Unit> units = new List<Unit>();
        static readonly StringBuilder sb = new StringBuilder();
        static readonly List<string> pieces = new List<string>();

        /// <summary>Lay the text out as it is even in a right-to-left language (other languages' names in the language list).</summary>
        public bool KeepOrder;

        int designSize;   // the size the layout asked for, before any shrinking
        int fitted;       // the size the last fit chose (anything else was set by the layout since)

        struct Unit
        {
            public string Text;
            public float Width;
        }

        public override string text
        {
            get => base.text;
            set
            {
                if (value != m_Text) Refit();
                base.text = value;
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Refit();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            Refit();
        }

        void Refit()
        {
            if (!Loc.IsEnglish) pending.Add(this);
        }

        /// <summary>Called once a frame (LocUpdater, before the canvases are drawn) for the labels that changed.</summary>
        internal static void FitPending()
        {
            if (pending.Count == 0) return;
            var batch = new List<LocText>(pending);
            pending.Clear();
            foreach (var t in batch)
                if (t != null && t.isActiveAndEnabled) t.Fit();
                else if (t != null) pending.Add(t);   // fit when it's shown
        }

        void Fit()
        {
            if (designSize == 0 || fontSize != fitted) designSize = fontSize;
            int size = designSize;
            var rect = rectTransform.rect;
            if (!string.IsNullOrEmpty(m_Text) && rect.width > 2f && font != null)
            {
                int min = Mathf.Max(10, Mathf.RoundToInt(designSize * 0.6f));
                for (int i = 0; i < 10 && size > min && !Fits(size, rect); i++)
                    size = Mathf.Max(min, Mathf.FloorToInt(size * 0.92f));
            }
            fitted = size;
            if (fontSize != size) fontSize = size;
        }

        bool Fits(int size, Rect rect)
        {
            var settings = GetGenerationSettings(Vector2.zero);
            settings.fontSize = size;
            settings.resizeTextForBestFit = false;
            settings.horizontalOverflow = HorizontalWrapMode.Overflow;
            float scale = 1f / pixelsPerUnit;
            if (horizontalOverflow == HorizontalWrapMode.Overflow)
                return measure.GetPreferredWidth(m_Text, settings) * scale <= rect.width + 1f;
            // Wrapped text: every word must fit on a line, and the lines in the height the layout gave it (at least one).
            foreach (var word in m_Text.Split(' ', '\n'))
                if (word.Length > 0 && word.IndexOf('<') < 0 && measure.GetPreferredWidth(word, settings) * scale > rect.width + 1f)
                    return false;
            settings = GetGenerationSettings(new Vector2(rect.width, 0f));
            settings.fontSize = size;
            settings.resizeTextForBestFit = false;
            float height = measure.GetPreferredHeight(m_Text, settings) * scale;
            float oneLine = size * lineSpacing * 1.35f;
            return height <= Mathf.Max(rect.height, oneLine) * 1.06f;
        }

        protected override void OnPopulateMesh(VertexHelper toFill)
        {
            if (!Loc.RightToLeft || KeepOrder || string.IsNullOrEmpty(m_Text) || font == null)
            {
                base.OnPopulateMesh(toFill);
                return;
            }
            string logical = m_Text;
            m_Text = RightToLeftLines(logical);
            try
            {
                base.OnPopulateMesh(toFill);
            }
            finally
            {
                m_Text = logical;
            }
        }

        /// <summary>
        /// Breaks the text into lines that fit (words in reading order), then writes each line's words from right to
        /// left. Rich-text tags are carried onto every word they cover, so they stay balanced after the reordering.
        /// </summary>
        string RightToLeftLines(string logical)
        {
            float width = horizontalOverflow == HorizontalWrapMode.Wrap ? rectTransform.rect.width * 0.98f : float.MaxValue;
            var settings = GetGenerationSettings(Vector2.zero);
            settings.horizontalOverflow = HorizontalWrapMode.Overflow;
            settings.resizeTextForBestFit = false;
            float scale = 1f / pixelsPerUnit;
            float space = measure.GetPreferredWidth("a a", settings) * scale - measure.GetPreferredWidth("aa", settings) * scale;

            var result = new StringBuilder(logical.Length + 32);
            var open = new List<string>();
            var paragraphs = logical.Split('\n');
            for (int p = 0; p < paragraphs.Length; p++)
            {
                if (p > 0) result.Append('\n');
                SplitUnits(paragraphs[p], open, settings, scale);
                int start = 0;
                float used = 0f;
                for (int i = 0; i <= units.Count; i++)
                {
                    bool end = i == units.Count;
                    if (!end)
                    {
                        float w = units[i].Width + (i > start ? space : 0f);
                        if (i == start || used + w <= width)
                        {
                            used += w;
                            continue;
                        }
                    }
                    // Units start..i-1 make one line: write them right to left.
                    if (start > 0) result.Append('\n');
                    for (int k = i - 1; k >= start; k--)
                    {
                        result.Append(units[k].Text);
                        if (k > start) result.Append(' ');
                    }
                    start = i;
                    used = end ? 0f : units[i].Width;
                }
            }
            return result.ToString();
        }

        /// <summary>
        /// The words of a paragraph, each wrapped in the rich-text tags open at that point. Pieces glued with U+200B (a
        /// value put into a sentence at run time, and the brackets or quotes round it) stay one word, laid out right to
        /// left like the rest.
        /// </summary>
        void SplitUnits(string paragraph, List<string> open, TextGenerationSettings settings, float scale)
        {
            units.Clear();
            sb.Clear();
            pieces.Clear();
            int i = 0;
            while (i <= paragraph.Length)
            {
                char c = i < paragraph.Length ? paragraph[i] : ' ';
                if (c == '<' && supportRichText)
                {
                    int close = paragraph.IndexOf('>', i);
                    if (close > i)
                    {
                        string tag = paragraph.Substring(i, close - i + 1);
                        if (tag.StartsWith("</")) { if (open.Count > 0) open.RemoveAt(open.Count - 1); }
                        else open.Add(tag);
                        i = close + 1;
                        continue;
                    }
                }
                if (c == '\u200b')
                {
                    if (sb.Length > 0) pieces.Add(Wrap(sb.ToString(), open));
                    sb.Clear();
                    i++;
                    continue;
                }
                if (c == ' ')
                {
                    if (sb.Length > 0) pieces.Add(Wrap(sb.ToString(), open));
                    sb.Clear();
                    if (pieces.Count > 0)
                    {
                        pieces.Reverse();
                        string word = string.Concat(pieces);
                        units.Add(new Unit { Text = word, Width = measure.GetPreferredWidth(word, settings) * scale });
                        pieces.Clear();
                    }
                    i++;
                    continue;
                }
                sb.Append(c);
                i++;
            }
        }

        static string Wrap(string word, List<string> open)
        {
            if (open.Count == 0) return word;
            var w = new StringBuilder();
            foreach (var tag in open) w.Append(tag);
            w.Append(word);
            for (int k = open.Count - 1; k >= 0; k--)
            {
                string tag = open[k];
                int end = tag.IndexOfAny(new[] { '=', '>' });
                w.Append("</").Append(tag, 1, end - 1).Append('>');
            }
            return w.ToString();
        }
    }

    /// <summary>Runs the pending LocText fits once a frame, before the canvases rebuild.</summary>
    public sealed class LocUpdater : MonoBehaviour
    {
        void LateUpdate() => LocText.FitPending();
    }
}
