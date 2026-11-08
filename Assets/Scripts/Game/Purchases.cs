using System;
using System.Collections;
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

        public bool CanWatchAd => AdsSupported && !app.Ads.IsBlocked && AdsLeftToday > 0 && AdCooldownRemaining <= 0;

        /// <summary>A video can be shown right now (allowed today and one has loaded).</summary>
        public bool AdReady => CanWatchAd && app.Ads.IsReady;

        public string AdStatus()
        {
            if (!AdsSupported) return Loc.T("Not available on this device");
            if (app.Ads.IsBlocked) return Loc.T("Videos can't load on this network");
            if (AdsLeftToday <= 0) return Loc.T("Come back tomorrow");
            double cd = AdCooldownRemaining;
            if (cd > 0) return Loc.F("Ready in {0}s", Mathf.CeilToInt((float)cd));
            return Loc.F("+{0} chips ({1} left today)", ChipFormat.Full(GameConfig.AdRewardChips), AdsLeftToday);
        }

        public void WatchAdForChips(Action<bool> done = null) => WatchAd(() =>
        {
            app.Wallet.Credit(GameConfig.AdRewardChips);
            Sfx.Play(SfxId.Coins);
            app.UI.Toast(Loc.F("+{0} free chips!", ChipFormat.Full(GameConfig.AdRewardChips)), Casino.Art.Palette.Gold, Casino.Art.Icon.Gift);
        }, done);

        /// <summary>A rewarded video for a streak freeze (counts towards the same daily allowance as free chips).</summary>
        public void WatchAdForFreeze(Action<bool> done = null) => WatchAd(() =>
        {
            app.Progress.AddFreeze();
            Sfx.Play(SfxId.Ding);
            app.UI.Toast(Loc.T("+1 streak freeze!"), Casino.Art.Palette.Hex("8FD8FF"), Casino.Art.Icon.Freeze);
        }, done);

        void WatchAd(Action reward, Action<bool> done)
        {
            if (!CanWatchAd)
            {
                app.UI.Toast(AdStatus(), Casino.Art.Palette.TextMuted, Casino.Art.Icon.Clock);
                done?.Invoke(false);
                return;
            }
            if (!app.Ads.IsReady)
            {
                app.UI.Toast(Loc.T("No video available right now. Try again in a moment."), Casino.Art.Palette.TextMuted, Casino.Art.Icon.Video);
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
                    reward();
                    app.SaveSystem.SaveNow();
                }
                done?.Invoke(earned);
            });
        }

        /// <summary>How many videos a bundle asks for: 1 to 3, never more than today's allowance has left.</summary>
        public int RollAdBundle() => 1 + app.Rng.Next(Math.Max(1, Math.Min(GameConfig.AdBundleMax, AdsLeftToday)));

        /// <summary>
        /// A bundle of rewarded videos for one prize. Every one must be watched to the end: closing one early loses the
        /// prize. If a later video can't be loaded, that isn't the player's doing, so the prize is given anyway.
        /// </summary>
        public void WatchAdBundle(int count, Action reward, Action<bool> done)
        {
            if (!CanWatchAd)
            {
                app.UI.Toast(AdStatus(), Casino.Art.Palette.TextMuted, Casino.Art.Icon.Clock);
                done?.Invoke(false);
                return;
            }
            app.StartCoroutine(AdBundle(count, reward, done));
        }

        IEnumerator AdBundle(int count, Action reward, Action<bool> done)
        {
            for (int k = 1; k <= count; k++)
            {
                float wait = 10f;
                while (!app.Ads.IsReady && wait > 0f)
                {
                    wait -= Time.unscaledDeltaTime;
                    yield return null;
                }
                if (!app.Ads.IsReady)
                {
                    if (k == 1)
                    {
                        app.UI.Toast(Loc.T("No video available right now. Try again in a moment."), Casino.Art.Palette.TextMuted, Casino.Art.Icon.Video);
                        done?.Invoke(false);
                        yield break;
                    }
                    break;   // the network let a video down, not the player: the prize stands
                }
                if (count > 1) app.UI.Toast(Loc.F("Video {0} of {1}", k, count), Casino.Art.Palette.TextMuted, Casino.Art.Icon.Video);
                bool? earned = null;
                app.Music.SetPaused(true);
                app.Ads.ShowRewarded(e => earned = e);
                while (earned == null) yield return null;
                app.Music.SetPaused(false);
                if (earned == false && app.Ads.LastShowFailed)
                {
                    // The video never played: the network's doing, not the player's.
                    if (k == 1)
                    {
                        app.UI.Toast(Loc.T("No video available right now. Try again in a moment."), Casino.Art.Palette.TextMuted, Casino.Art.Icon.Video);
                        done?.Invoke(false);
                        yield break;
                    }
                    break;
                }
                if (earned == false)
                {
                    app.UI.Toast(Loc.T("Prize lost: the videos have to be watched all the way through."), Casino.Art.Palette.Red, Casino.Art.Icon.Close);
                    app.SaveSystem.SaveNow();
                    done?.Invoke(false);
                    yield break;
                }
                RollAdDay();
                app.Save.adsWatchedToday++;
            }
            app.Save.lastAdUtcTicks = DateTime.UtcNow.Ticks;
            reward();
            app.SaveSystem.SaveNow();
            done?.Invoke(true);
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
