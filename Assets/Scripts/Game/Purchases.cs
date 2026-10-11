using System;
using System.Collections.Generic;
using Casino.Audio;
using Casino.Core;
using Casino.Core.Economy;
using Casino.Platform;
using UnityEngine;

namespace Casino
{
    /// <summary>Turns store purchases and rewarded ads into chips and energy, exactly once per transaction.</summary>
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
            return RegionalPricing.FormatUsd(RegionalPricing.PriceUsd(p, app.Region.PricingCountry, GameConfig.Pricing));
        }

        public void Buy(ProductDef p, Action<PurchaseResult> done = null)
        {
            if (!app.Store.IsAvailable)
            {
                app.UI.Toast("Purchases are not available in this version.", Casino.Art.Palette.Red, Casino.Art.Icon.Lock);
                done?.Invoke(PurchaseResult.Unavailable);
                return;
            }
            app.Store.Purchase(SkuFor(p), result =>
            {
                if (result == PurchaseResult.Failed) app.UI.Toast("Purchase failed. You were not charged.", Casino.Art.Palette.Red, Casino.Art.Icon.Close);
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
                app.UI.Toast($"+{ChipFormat.Full(product.Chips)} chips added!", Casino.Art.Palette.Gold, Casino.Art.Icon.Coin);
            }
            else
            {
                app.Energy.Add(product.Energy);
                Sfx.Play(SfxId.Eat);
                app.UI.Toast($"{product.Title}: +{Mathf.RoundToInt(product.Energy * 100)}% energy", Casino.Art.Palette.Energy, Casino.Art.Icon.Bolt);
            }
            save.MarkGranted(transactionId);
            app.SaveSystem.SaveNow();
            Granted?.Invoke(product);
            return true;
        }

        // ---------------- free chips from rewarded ads ----------------

        public bool AdsSupported => app.Ads.IsSupported;

        public int AdsLeftToday
        {
            get
            {
                RollAdDay();
                return Math.Max(0, GameConfig.AdsPerDay - app.Save.adsWatchedToday);
            }
        }

        public double AdCooldownRemaining
        {
            get
            {
                var since = DateTime.UtcNow - new DateTime(app.Save.lastAdUtcTicks, DateTimeKind.Utc);
                return Math.Max(0, GameConfig.AdCooldownSeconds - since.TotalSeconds);
            }
        }

        public bool CanWatchAd => AdsSupported && AdsLeftToday > 0 && AdCooldownRemaining <= 0;

        public string AdStatus()
        {
            if (!AdsSupported) return "Not available on this device";
            if (AdsLeftToday <= 0) return "Come back tomorrow";
            double cd = AdCooldownRemaining;
            if (cd > 0) return $"Ready in {Mathf.CeilToInt((float)cd)}s";
            return $"+{ChipFormat.Full(GameConfig.AdRewardChips)} chips ({AdsLeftToday} left today)";
        }

        public void WatchAdForChips(Action<bool> done = null)
        {
            if (!CanWatchAd)
            {
                app.UI.Toast(AdStatus(), Casino.Art.Palette.TextMuted, Casino.Art.Icon.Clock);
                done?.Invoke(false);
                return;
            }
            if (!app.Ads.IsReady)
            {
                app.UI.Toast("No video available right now. Try again in a moment.", Casino.Art.Palette.TextMuted, Casino.Art.Icon.Video);
                done?.Invoke(false);
                return;
            }
            app.Music.SetPaused(true);
            app.Ads.ShowRewarded(earned =>
            {
                app.Music.SetPaused(false);
                if (earned)
                {
                    RollAdDay();
                    app.Save.adsWatchedToday++;
                    app.Save.lastAdUtcTicks = DateTime.UtcNow.Ticks;
                    app.Wallet.Credit(GameConfig.AdRewardChips);
                    app.SaveSystem.SaveNow();
                    Sfx.Play(SfxId.Coins);
                    app.UI.Toast($"+{ChipFormat.Full(GameConfig.AdRewardChips)} free chips!", Casino.Art.Palette.Gold, Casino.Art.Icon.Gift);
                }
                done?.Invoke(earned);
            });
        }

        void RollAdDay()
        {
            int today = (int)(DateTime.UtcNow.Ticks / TimeSpan.TicksPerDay);
            if (app.Save.adDayNumber == today) return;
            app.Save.adDayNumber = today;
            app.Save.adsWatchedToday = 0;
        }
    }
}
