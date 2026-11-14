using System;
using System.Collections.Generic;
using Casino.Audio;
using Casino.Core;
using Casino.Core.Economy;
using Casino.Platform;
using UnityEngine;

namespace Casino
{
    /// <summary>Turns store purchases into chips and energy, exactly once per transaction.</summary>
    public sealed class Purchases
    {
        readonly App app;

        public Purchases(App app) => this.app = app;

        public event Action<ProductDef> Granted;

        public IReadOnlyList<string> AllSkus()
        {
            var skus = new List<string>();
            foreach (var p in ProductCatalog.All)
            {
                if (GameConfig.Pricing == PricingMode.LocationTiers)
                    for (int t = 1; t <= RegionalPricing.TierCount; t++) skus.Add(p.Id + "_t" + t);
                else skus.Add(p.Id);
            }
            return skus;
        }

        public string SkuFor(ProductDef p) => RegionalPricing.StoreSku(p, app.Region.PricingCountry, GameConfig.Pricing);

        /// <summary>The store's own localized price when known (that is what gets charged), else our regional USD estimate.</summary>
        public string DisplayPrice(ProductDef p)
        {
            string store = app.Store.LocalizedPrice(SkuFor(p));
            if (!string.IsNullOrEmpty(store)) return store;
            return LocalPrice(p, app.Region.PricingCountry);
        }

        /// <summary>A product's regional price in a country's own money, written the way the player's language writes prices.</summary>
        public static string LocalPrice(ProductDef p, string country) =>
            RegionalPricing.Format(RegionalPricing.LocalPrice(p, country, GameConfig.Pricing), RegionalPricing.Currency(country), Loc.PriceStyle);

        public void Buy(ProductDef p, Action<PurchaseResult> done = null)
        {
            if (!app.Store.IsAvailable)
            {
                app.UI.Toast(Loc.T("Purchases are not available in this version."), Casino.Art.Palette.Red, Casino.Art.Icon.Lock);
                done?.Invoke(PurchaseResult.Unavailable);
                return;
            }
            app.Store.Purchase(SkuFor(p), result =>
            {
                if (result == PurchaseResult.Failed) app.UI.Toast(Loc.T("Purchase failed. You were not charged."), Casino.Art.Palette.Red, Casino.Art.Icon.Close);
                done?.Invoke(result);
            });
        }

        /// <summary>Store callback. Returns true once the product is granted (or was granted before).</summary>
        public bool Fulfill(string sku, string transactionId)
        {
            var product = ProductCatalog.Find(RegionalPricing.ProductIdFromSku(sku));
            if (product == null)
            {
                Debug.LogError("Unknown product purchased: " + sku);
                return false;
            }
            var save = app.Save;
            if (save.HasGranted(transactionId)) return true;

            if (product.Kind == ProductKind.Chips)
            {
                app.Wallet.Credit(product.Chips);
                Sfx.Play(SfxId.Coins);
                app.UI.Toast(Loc.F("+{0} chips added!", ChipFormat.Full(product.Chips)), Casino.Art.Palette.Gold, Casino.Art.Icon.Coin);
            }
            else if (product.Kind == ProductKind.Luck)
            {
                app.Boost.Add(product.LuckHands, product.LuckPercent);
                Sfx.Play(SfxId.Ding);
                app.UI.Toast(app.Luck.IsActive
                        ? Loc.F("{0}: {1} hands at {2}% luck, after Beginner's Luck", Loc.T(product.Title), product.LuckHands, product.LuckPercent)
                        : Loc.F("{0}: {1} hands at {2}% luck!", Loc.T(product.Title), product.LuckHands, product.LuckPercent),
                    Casino.Art.Palette.Hex("7CFC9A"), Casino.Art.Icon.Sparkle);
            }
            else
            {
                app.Energy.Add(product.Energy);
                Sfx.Play(SfxId.Eat);
                app.UI.Toast(Loc.F("{0}: +{1}% energy", Loc.T(product.Title), Mathf.RoundToInt(product.Energy * 100)), Casino.Art.Palette.Energy, Casino.Art.Icon.Bolt);
            }
            save.MarkGranted(transactionId);
            app.SaveSystem.SaveNow();
            Granted?.Invoke(product);
            return true;
        }
    }
}
