using System.Collections.Generic;
using Casino.Art;
using Casino.Audio;
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
    public enum ShopTab { Chips, Food, Luck }

    /// <summary>
    /// The cashier (chip packs), the bar (food and drinks that refill energy) and luck packs (a Lucky Streak of boosted
    /// blackjack hands). Prices are regional.
    /// </summary>
    public sealed class ShopModal : UIModal
    {
        public ShopTab StartTab = ShopTab.Chips;

        RectTransform grid, staffHolder;
        UIButton chipsTab, foodTab, luckTab;
        Text regionText, staffLine;
        ShopTab tab;

        protected override void Build()
        {
            MakePanel(1760, 900);
            AddCloseButton();

            chipsTab = UIKit.Button(Panel, Loc.T("CASHIER"), ButtonStyle.Gold, () => ShowTab(ShopTab.Chips), 44, Icon.Coin);
            chipsTab.Rect.At(0.5f, 1f, -420, -70, 370, 96);
            foodTab = UIKit.Button(Panel, Loc.T("THE BAR"), ButtonStyle.Dark, () => ShowTab(ShopTab.Food), 44, Icon.Food);
            foodTab.Rect.At(0.5f, 1f, -20, -70, 370, 96);
            luckTab = UIKit.Button(Panel, Loc.T("LUCK"), ButtonStyle.Dark, () => ShowTab(ShopTab.Luck), 44, Icon.Sparkle);
            luckTab.Rect.At(0.5f, 1f, 380, -70, 370, 96);

            staffHolder = UIKit.NewRect("Staff", Panel).At(0f, 0.5f, 210, 40, 300, 560);
            staffLine = UIKit.Label(Panel, "", 26, Color.white, TextAnchor.UpperCenter, FontKind.Semi);
            ((RectTransform)staffLine.transform).At(0f, 0.5f, 210, -104, 330, 84);   // under the name badge, over the ad tile

            grid = UIKit.NewRect("Grid", Panel).At(0.5f, 0.5f, 170, -20, 1300, 600);

            regionText = UIKit.Label(Panel, "", 22, Palette.TextMuted, TextAnchor.MiddleCenter);
            ((RectTransform)regionText.transform).At(0.5f, 0f, 0, 52, 1600, 34);
            var legal = UIKit.Label(Panel, Loc.T("Chips, luck and food are virtual items for entertainment only: no cash value, not redeemable for money or prizes."),
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
            luckTab.SetStyle(t == ShopTab.Luck ? ButtonStyle.Gold : ButtonStyle.Dark);
            foreach (Transform c in grid) Destroy(c.gameObject);
            foreach (Transform c in staffHolder) Destroy(c.gameObject);

            var role = t == ShopTab.Food ? StaffRole.Server : StaffRole.Cashier;
            var staff = Staff.Portrait(staffHolder, 260, role);
            staff.Rect.Center(0, 60, 260, 260);
            staffLine.text = Loc.F("“{0}”", DealerLines.Greeting(role, App.I.Rng));
            staff.Talk(1.2f);
            AdTile(t);

            if (t == ShopTab.Luck)
            {
                var packs = ProductCatalog.OfKind(ProductKind.Luck);
                for (int i = 0; i < packs.Count; i++) LuckCard(grid, packs[i], new Vector2((i - (packs.Count - 1) * 0.5f) * 460f, 120f));
                var how = UIKit.Label(grid,
                    Loc.F("Each card has that chance of being dealt in your favor, on blackjack bets up to {0} chips " +
                          "(bigger bets are standard and don't use up hands). Starts after Beginner's Luck; stronger packs are used first.",
                          ChipFormat.Full(GameConfig.LuckBoostMaxBet)),
                    24, Palette.TextMuted, TextAnchor.UpperCenter, FontKind.Body);
                ((RectTransform)how.transform).Center(0, -150, 1100, 120);
            }
            else
            {
                var products = ProductCatalog.OfKind(t == ShopTab.Chips ? ProductKind.Chips : ProductKind.Food);
                for (int i = 0; i < products.Count; i++)
                {
                    int col = i % 3, row = i / 3;
                    BuildItem(products[i], new Vector2(-430 + col * 430, 155 - row * 320));
                }
            }

            var region = App.I.Region;
            string where = region.Describe();
            if (region.LocationDeclined)
                where += "  •  " + Loc.T("Allow location access to get your country's price");
            if (!App.I.Store.IsAvailable) where = Loc.T("Purchases are not available in this version of the game.");
            else if (App.I.Store is SimulatedStore) where += "  •  " + Loc.T("TEST STORE (no real money)");
            regionText.text = where;
        }

        // ---------------- watch ads for a prize ----------------

        // The number of videos is rolled on the first tap and kept until it's watched, so backing out doesn't reroll it.
        static readonly Dictionary<ShopTab, int> pendingAds = new Dictionary<ShopTab, int>();

        static string AdPrize(ShopTab t) => t == ShopTab.Chips ? Loc.F("+{0} chips", ChipFormat.Full(GameConfig.AdBundleChips))
            : t == ShopTab.Food ? Loc.F("+{0}% energy", Mathf.RoundToInt(GameConfig.AdBundleEnergy * 100))
            : Loc.F("{0} hands at {1}% luck", GameConfig.AdBundleLuckHands, GameConfig.AdBundleLuckPercent);

        /// <summary>One tile per section, under the cashier or bartender: watch 1 to 3 videos for that section's prize.</summary>
        void AdTile(ShopTab t)
        {
            var card = UIKit.Panel(staffHolder, "Ads", Palette.PanelLight, 24f, true);
            var rt = ((RectTransform)card.transform).Center(0, -300, 330, 200);
            UIKit.Outline(rt, Palette.WithAlpha(Palette.Gold, 0.45f), 24f);
            var icon = UIKit.Image(rt, "Icon", ProceduralArt.IconSprite(Icon.Video), Color.white);
            icon.preserveAspect = true;
            ((RectTransform)icon.transform).Center(-118, 64, 46, 46);
            var title = UIKit.Label(rt, Loc.T("WATCH ADS"), 32, Color.white, TextAnchor.MiddleLeft, FontKind.Display);
            ((RectTransform)title.transform).Center(28, 64, 236, 40);
            // the prize gets the tile's full width: "5 hands at 25% luck" doesn't fit beside the icon
            var prize = UIKit.Label(rt, AdPrize(t), 22, Palette.Gold, TextAnchor.MiddleCenter, FontKind.Semi);
            prize.horizontalOverflow = HorizontalWrapMode.Overflow;
            ((RectTransform)prize.transform).Center(0, 26, 310, 30);

            var purchases = App.I.Purchases;
            bool can = purchases.CanWatchAd;
            string note = !purchases.AdsSupported ? Loc.T("Not available on this device")
                : App.I.Ads.IsBlocked ? Loc.T("Videos can't load on this network")
                : purchases.AdsLeftToday <= 0 ? Loc.T("Come back tomorrow")
                : purchases.AdCooldownRemaining > 0 ? Loc.F("Ready in {0}s", Mathf.CeilToInt((float)purchases.AdCooldownRemaining))
                : Loc.T("1 to 3 short videos");
            var n = UIKit.Label(rt, note, 19, Palette.TextMuted, TextAnchor.MiddleCenter, FontKind.Body);
            ((RectTransform)n.transform).Center(0, -4, 310, 26);

            var watch = UIKit.Button(rt, Loc.T("WATCH"), ButtonStyle.Green, () => StartAds(t), 32, Icon.Play);
            watch.Rect.At(0.5f, 0f, 0, 44, 290, 66);
            watch.Interactable = can;
        }

        void StartAds(ShopTab t)
        {
            if (!pendingAds.TryGetValue(t, out int count)) pendingAds[t] = count = App.I.Purchases.RollAdBundle();
            string prize = AdPrize(t);
            App.I.UI.Modal<ConfirmModal>(m => m.Setup(
                count == 1 ? Loc.T("WATCH 1 AD?") : Loc.F("WATCH {0} ADS?", count),
                count == 1 ? Loc.F("Watch 1 video all the way through to get {0}. If you stop early, the prize is lost.", prize)
                           : Loc.F("Watch {0} videos all the way through to get {1}. If you stop early, the prize is lost.", count, prize),
                Loc.T("START"), () => App.I.Purchases.WatchAdBundle(count, () => GrantAdPrize(t), ok =>
                {
                    pendingAds.Remove(t);   // won or lost, the next bundle rolls afresh
                    if (this != null) ShowTab(tab);
                }), Loc.T("NOT NOW")));
        }

        static void GrantAdPrize(ShopTab t)
        {
            var app = App.I;
            if (t == ShopTab.Chips)
            {
                app.Wallet.Credit(GameConfig.AdBundleChips);
                Sfx.Play(SfxId.Coins);
            }
            else if (t == ShopTab.Food)
            {
                app.Energy.Add(GameConfig.AdBundleEnergy);
                Sfx.Play(SfxId.Eat);
            }
            else
            {
                app.Boost.Add(GameConfig.AdBundleLuckHands, GameConfig.AdBundleLuckPercent);
                Sfx.Play(SfxId.Ding);
            }
            app.UI.Toast(Loc.F("Prize won: {0}!", AdPrize(t)), Palette.Gold, Icon.Gift);
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

            string amount = chips ? ChipFormat.Short(p.Chips) : "+" + Loc.F("{0}%", Mathf.RoundToInt(p.Energy * 100));
            var a = UIKit.Label(rt, amount, 64, chips ? Palette.Gold : Palette.Energy, TextAnchor.MiddleLeft, FontKind.Display);
            ((RectTransform)a.transform).Center(85, 80, 210, 70);
            UIKit.TextShadow(a, -3f, 0.6f);
            var name = UIKit.Label(rt, chips ? Loc.T(p.Title) : Loc.T(p.Title) + "\n<size=20><color=#C9BFDB>" + Loc.T("energy") + "</color></size>", 24, Color.white, TextAnchor.UpperLeft, FontKind.Semi);
            ((RectTransform)name.transform).Center(85, 16, 210, 66);

            int bonus = ProductCatalog.BonusPercent(p);
            string badge = !string.IsNullOrEmpty(p.Highlight) ? Loc.T(p.Highlight) : bonus > 0 ? Loc.F("+{0}% CHIPS", bonus) : null;
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

        /// <summary>A luck pack: its boost, hands and expected win rate, and its price. Also used by the offer.</summary>
        public static void LuckCard(Transform parent, ProductDef p, Vector2 pos)
        {
            var green = Palette.Hex("7CFC9A");
            var card = UIKit.Panel(parent, p.Id, Palette.PanelLight, 28f, true);
            var rt = ((RectTransform)card.transform).Center(pos.x, pos.y, 420, 330);
            UIKit.Outline(rt, string.IsNullOrEmpty(p.Highlight) ? new Color(1, 1, 1, 0.12f) : green, 28f);
            var glow = UIKit.Image(rt, "Glow", ProceduralArt.Glow, Palette.WithAlpha(green, 0.18f));
            ((RectTransform)glow.transform).Center(-120, 60, 260, 260);
            var icon = UIKit.Image(rt, "Icon", ProceduralArt.IconSprite(Icon.Sparkle), green);
            ((RectTransform)icon.transform).Center(-120, 70, 120, 120);

            var a = UIKit.Label(rt, Loc.F("{0}%", p.LuckPercent), 72, green, TextAnchor.MiddleLeft, FontKind.Display);
            ((RectTransform)a.transform).Center(80, 112, 230, 76);
            UIKit.TextShadow(a, -3f, 0.6f);
            var name = UIKit.Label(rt, Loc.T(p.Title) + "\n<size=21><color=#C9BFDB>" + Loc.F("{0} hands", p.LuckHands) + "\n" +
                Loc.F("wins about {0}% of them", Mathf.RoundToInt((float)LuckBoost.WinRate(p.LuckPercent) * 100)) + "</color></size>",
                26, Color.white, TextAnchor.UpperLeft, FontKind.Semi);
            ((RectTransform)name.transform).Center(80, 24, 230, 100);

            if (!string.IsNullOrEmpty(p.Highlight))
            {
                var b = UIKit.Panel(rt, "Badge", Palette.Pink, 18f);
                var brt = ((RectTransform)b.transform).At(0.5f, 1f, 0, 14, 230, 40);
                var bt = UIKit.Label(brt, Loc.T(p.Highlight), 22, Color.white, TextAnchor.MiddleCenter, FontKind.Bold);
                ((RectTransform)bt.transform).Stretch();
            }

            var buy = UIKit.Button(rt, App.I.Purchases.DisplayPrice(p), ButtonStyle.Green, () => App.I.Purchases.Buy(p), 40, null, FontKind.Bold);
            buy.Rect.At(0.5f, 0f, 0, 62, 340, 96);
            buy.Interactable = App.I.Store.IsAvailable;
        }

        static Icon ChipIcon(ProductDef p) => p.Chips >= 200_000 ? Icon.Crown : p.Chips >= 30_000 ? Icon.Chips : Icon.Coin;
    }
}
