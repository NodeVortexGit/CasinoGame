using System;
using System.Collections.Generic;
using Casino.Art;
using Casino.Core.Economy;
using Casino.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Casino.Screens
{
    /// <summary>
    /// A long list of choices as pages of a grid, with previous and next buttons: works the same with touch, a mouse and
    /// a controller (no scrolling), and opens on the page with the current choice.
    /// </summary>
    public abstract class PickerModal : UIModal
    {
        protected const int Columns = 4, Rows = 8, PerPage = Columns * Rows;

        RectTransform grid;
        Text pageText;
        UIButton previous, next;
        int page;

        protected abstract string Title { get; }
        protected abstract int Count { get; }
        protected abstract int CurrentIndex { get; }

        /// <summary>Fills in the choice's button (label, style); <paramref name="current"/> is the one in use.</summary>
        protected abstract void Describe(int index, UIButton button, bool current);

        protected abstract void Pick(int index);

        public override GameObject DefaultFocus
        {
            get
            {
                int current = CurrentIndex;
                if (current / PerPage != page || grid == null) return null;
                int slot = current % PerPage;
                return slot < grid.childCount ? grid.GetChild(slot).gameObject : null;
            }
        }

        protected override void Build()
        {
            MakePanel(1760, 1000, Title);
            AddCloseButton();
            grid = UIKit.NewRect("Grid", Panel).Center(0, -10, 1640, 760);
            previous = UIKit.Button(Panel, "‹", ButtonStyle.Dark, () => ShowPage(page - 1), 56, null, FontKind.Bold);
            previous.Rect.At(0.5f, 0f, -330, 70, 160, 90);
            next = UIKit.Button(Panel, "›", ButtonStyle.Dark, () => ShowPage(page + 1), 56, null, FontKind.Bold);
            next.Rect.At(0.5f, 0f, 330, 70, 160, 90);
            pageText = UIKit.Label(Panel, "", 30, Palette.TextMuted, TextAnchor.MiddleCenter, FontKind.Semi);
            ((RectTransform)pageText.transform).At(0.5f, 0f, 0, 70, 420, 60);
            ShowPage(Mathf.Max(0, CurrentIndex) / PerPage);
        }

        int Pages => Mathf.Max(1, (Count + PerPage - 1) / PerPage);

        void ShowPage(int p)
        {
            page = Mathf.Clamp(p, 0, Pages - 1);
            for (int i = grid.childCount - 1; i >= 0; i--) Destroy(grid.GetChild(i).gameObject);
            grid.DetachChildren();
            const float w = 1640f / Columns, h = 760f / Rows;
            int current = CurrentIndex;
            for (int slot = 0; slot < PerPage; slot++)
            {
                int index = page * PerPage + slot;
                if (index >= Count) break;
                int col = slot % Columns, row = slot / Columns;
                var b = UIKit.Button(grid, " ", index == current ? ButtonStyle.Gold : ButtonStyle.Dark, () => Pick(index), 28, null,
                    FontKind.Semi, 18f);
                b.Rect.Center(-820 + w * (col + 0.5f), 380 - h * (row + 0.5f), w - 16, h - 12);
                Describe(index, b, index == current);
            }
            pageText.text = Loc.F("Page {0} of {1}", page + 1, Pages);
            previous.Interactable = page > 0;
            next.Interactable = page < Pages - 1;
        }
    }

    /// <summary>The game's languages, each named in its own language (and in yours), plus following the device.</summary>
    public sealed class LanguagePickerModal : PickerModal
    {
        static List<LanguageInfo> sorted;

        static List<LanguageInfo> Languages
        {
            get
            {
                if (sorted != null) return sorted;
                sorted = new List<LanguageInfo>(LanguageTable.All);
                sorted.Sort((a, b) => string.CompareOrdinal(a.EnglishName, b.EnglishName));
                return sorted;
            }
        }

        protected override string Title => Loc.T("LANGUAGE");
        protected override int Count => Languages.Count + 1;
        protected override int CurrentIndex => string.IsNullOrEmpty(Loc.Choice) ? 0 : Languages.IndexOf(Loc.Current) + 1;

        protected override void Describe(int index, UIButton button, bool current)
        {
            var label = button.LabelText;
            if (index == 0)
            {
                label.text = Loc.T("AUTOMATIC");
                return;
            }
            var lang = Languages[index - 1];
            // Every language's own name, in the font that has them all, kept in its own direction.
            label.font = Fonts.LanguageNames;
            if (label is LocText lt) lt.KeepOrder = true;
            label.text = lang.NativeName;
            label.rectTransform.offsetMin = new Vector2(label.rectTransform.offsetMin.x, 26f);
            var name = UIKit.Label(button.LabelText.transform.parent, Loc.LanguageName(lang), 18,
                current ? Palette.TextDark : Palette.TextMuted, TextAnchor.LowerCenter, FontKind.Body);
            ((RectTransform)name.transform).Stretch(8, 6, 8, 0);
        }

        protected override void Pick(int index)
        {
            Close();
            Loc.Set(index == 0 ? "" : Languages[index - 1].Code);
        }
    }

    /// <summary>Test builds: every country, to see its currency and regional prices (Settings > Dev).</summary>
    public sealed class CountryPickerModal : PickerModal
    {
        /// <summary>The picked ISO country code, or null for the detected country.</summary>
        public Action<string> Picked;

        List<string> codes;

        List<string> Codes
        {
            get
            {
                if (codes != null) return codes;
                codes = new List<string>(RegionalPricing.Countries);
                codes.Sort((a, b) => string.Compare(Loc.Country(a), Loc.Country(b), StringComparison.CurrentCulture));
                return codes;
            }
        }

        protected override string Title => Loc.T("COUNTRY");
        protected override int Count => Codes.Count + 1;
        protected override int CurrentIndex
        {
            get
            {
                string chosen = App.I.Save.testCountry;
                return string.IsNullOrEmpty(chosen) ? 0 : Codes.IndexOf(chosen) + 1;
            }
        }

        protected override void Describe(int index, UIButton button, bool current)
        {
            var label = button.LabelText;
            if (index == 0)
            {
                label.text = Loc.T("AUTOMATIC");
                return;
            }
            string code = Codes[index - 1];
            label.text = Loc.Country(code);
            label.rectTransform.offsetMin = new Vector2(label.rectTransform.offsetMin.x, 26f);
            var price = UIKit.Label(button.LabelText.transform.parent,
                code + "  •  " + Purchases.LocalPrice(ProductCatalog.Find("chips_30k"), code), 18,
                current ? Palette.TextDark : Palette.TextMuted, TextAnchor.LowerCenter, FontKind.Body);
            ((RectTransform)price.transform).Stretch(8, 6, 8, 0);
        }

        protected override void Pick(int index)
        {
            Close();
            Picked?.Invoke(index == 0 ? null : Codes[index - 1]);
        }
    }
}
