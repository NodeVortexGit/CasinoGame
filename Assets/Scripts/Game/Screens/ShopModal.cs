using Casino.Art;
using Casino.Core;
using Casino.Core.Economy;
using Casino.Core.Player;
using Casino.Core.Region;
using Casino.Platform;
using Casino.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Casino.Screens
{
    public enum ShopTab { Chips, Food }

    /// <summary>The cashier (chip packs) and the bar (food and drinks that refill energy). Prices are regional.</summary>
    public sealed class ShopModal : UIModal
    {
        public ShopTab StartTab = ShopTab.Chips;

        RectTransform grid, staffHolder;
        UIButton chipsTab, foodTab;
        Text regionText, staffLine;
        ShopTab tab;

        protected override void Build()
        {
            MakePanel(1760, 900);
            AddCloseButton();

            chipsTab = UIKit.Button(Panel, "CASHIER", ButtonStyle.Gold, () => ShowTab(ShopTab.Chips), 44, Icon.Coin);
            chipsTab.Rect.At(0.5f, 1f, -230, -70, 400, 96);
            foodTab = UIKit.Button(Panel, "THE BAR", ButtonStyle.Dark, () => ShowTab(ShopTab.Food), 44, Icon.Food);
            foodTab.Rect.At(0.5f, 1f, 210, -70, 400, 96);

            staffHolder = UIKit.NewRect("Staff", Panel).At(0f, 0.5f, 210, 40, 300, 560);
            staffLine = UIKit.Label(Panel, "", 28, Color.white, TextAnchor.UpperCenter, FontKind.Semi);
            ((RectTransform)staffLine.transform).At(0f, 0.5f, 210, -230, 330, 160);

            grid = UIKit.NewRect("Grid", Panel).At(0.5f, 0.5f, 170, -20, 1300, 600);

            regionText = UIKit.Label(Panel, "", 22, Palette.TextMuted, TextAnchor.MiddleCenter);
            ((RectTransform)regionText.transform).At(0.5f, 0f, 0, 52, 1600, 34);
            var legal = UIKit.Label(Panel, "Chips and food are virtual items for entertainment only: no cash value, not redeemable for money or prizes.",
                20, Palette.WithAlpha(Palette.TextMuted, 0.75f), TextAnchor.MiddleCenter);
            ((RectTransform)legal.transform).At(0.5f, 0f, 0, 22, 1600, 30);

            App.I.Region.Changed += OnRegionChanged;
            ShowTab(StartTab);
        }

        void OnDestroy()
        {
            if (App.I != null) App.I.Region.Changed -= OnRegionChanged;
        }

        void OnRegionChanged() => ShowTab(tab);

        internal void TourTab(ShopTab t) => ShowTab(t);

        void ShowTab(ShopTab t)
        {
            tab = t;
            chipsTab.SetStyle(t == ShopTab.Chips ? ButtonStyle.Gold : ButtonStyle.Dark);
            foodTab.SetStyle(t == ShopTab.Food ? ButtonStyle.Gold : ButtonStyle.Dark);
            foreach (Transform c in grid) Destroy(c.gameObject);
            foreach (Transform c in staffHolder) Destroy(c.gameObject);

            var role = t == ShopTab.Chips ? StaffRole.Cashier : StaffRole.Server;
            var staff = Staff.Portrait(staffHolder, 260, role);
            staff.Rect.Center(0, 60, 260, 260);
            staffLine.text = "\"" + DealerLines.Greeting(role, App.I.Rng) + "\"";
            staff.Talk(1.2f);

            var products = ProductCatalog.OfKind(t == ShopTab.Chips ? ProductKind.Chips : ProductKind.Food);
            for (int i = 0; i < products.Count; i++)
            {
                int col = i % 3, row = i / 3;
                BuildItem(products[i], new Vector2(-430 + col * 430, 155 - row * 320));
            }

            var region = App.I.Region;
            string where = region.Describe();
            if (region.LocationDeclined)
                where += "  •  Allow location access to get your country's price";
            if (!App.I.Store.IsAvailable) where = "Purchases are not available in this version of the game.";
            else if (App.I.Store is SimulatedStore) where += "  •  TEST STORE (no real money)";
            regionText.text = where;
        }

        void BuildItem(ProductDef p, Vector2 pos)
        {
            bool chips = p.Kind == ProductKind.Chips;
            var card = UIKit.Panel(grid, p.Id, Palette.PanelLight, 28f, true);
            var rt = ((RectTransform)card.transform).Center(pos.x, pos.y, 400, 270);
            UIKit.Outline(rt, string.IsNullOrEmpty(p.Highlight) ? new Color(1, 1, 1, 0.12f) : Palette.Gold, 28f);
            var glow = UIKit.Image(rt, "Glow", ProceduralArt.Glow, Palette.WithAlpha(chips ? Palette.Gold : Palette.Energy, 0.18f));
            ((RectTransform)glow.transform).Center(-110, 30, 260, 260);

            var icon = UIKit.Image(rt, "Icon", chips ? ProceduralArt.IconSprite(ChipIcon(p)) : ProceduralArt.Food(p.Id), chips ? Palette.Gold : Color.white);
            icon.preserveAspect = true;
            ((RectTransform)icon.transform).Center(-110, 40, 140, 140);
            if (chips && ChipIcon(p) == Icon.Coin) icon.color = Color.white;

            string amount = chips ? ChipFormat.Short(p.Chips) : "+" + Mathf.RoundToInt(p.Energy * 100) + "%";
            var a = UIKit.Label(rt, amount, 64, chips ? Palette.Gold : Palette.Energy, TextAnchor.MiddleLeft, FontKind.Display);
            ((RectTransform)a.transform).Center(85, 80, 210, 70);
            UIKit.TextShadow(a, -3f, 0.6f);
            var name = UIKit.Label(rt, chips ? p.Title : p.Title + "\n<size=20><color=#C9BFDB>energy</color></size>", 24, Color.white, TextAnchor.UpperLeft, FontKind.Semi);
            ((RectTransform)name.transform).Center(85, 16, 210, 66);

            int bonus = ProductCatalog.BonusPercent(p);
            string badge = !string.IsNullOrEmpty(p.Highlight) ? p.Highlight : bonus > 0 ? $"+{bonus}% CHIPS" : null;
            if (badge != null)
            {
                var b = UIKit.Panel(rt, "Badge", Palette.Pink, 18f);
                var brt = ((RectTransform)b.transform).At(0.5f, 1f, 0, 14, 230, 40);
                var bt = UIKit.Label(brt, badge, 22, Color.white, TextAnchor.MiddleCenter, FontKind.Bold);
                ((RectTransform)bt.transform).Stretch();
            }

            var buy = UIKit.Button(rt, App.I.Purchases.DisplayPrice(p), ButtonStyle.Green, () => App.I.Purchases.Buy(p), 40, null, FontKind.Bold);
            buy.Rect.At(0.5f, 0f, 0, 62, 340, 96);
            buy.Interactable = App.I.Store.IsAvailable;
        }

        static Icon ChipIcon(ProductDef p) => p.Chips >= 200_000 ? Icon.Crown : p.Chips >= 30_000 ? Icon.Chips : Icon.Coin;
    }
}
