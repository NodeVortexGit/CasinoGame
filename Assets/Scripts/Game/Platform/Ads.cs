using System;
using UnityEngine;
#if CASINO_ADMOB && UNITY_ANDROID
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
#endif

namespace Casino.Platform
{
    public interface IAdService
    {
        bool IsSupported { get; }
        bool IsReady { get; }

        /// <summary>Videos keep failing to load (no connection, or an ad blocker on the network).</summary>
        bool IsBlocked { get; }
        void Initialize();
        /// <summary>Shows a rewarded video; <c>done(true)</c> only if the player earned the reward.</summary>
        void ShowRewarded(Action<bool> done);
    }

    public sealed class NoAds : IAdService
    {
        public bool IsSupported => false;
        public bool IsReady => false;
        public bool IsBlocked => false;
        public void Initialize() { }
        public void ShowRewarded(Action<bool> done) => done(false);
    }

    /// <summary>Editor / development builds: a 5-second placeholder instead of a real ad.</summary>
    public sealed class SimulatedAds : IAdService
    {
        public bool IsSupported => true;
        public bool IsReady => true;
        public bool IsBlocked => false;
        public void Initialize() { }
        public void ShowRewarded(Action<bool> done) => App.I.UI.Modal<Casino.Screens.SimulatedAdModal>(m => m.Setup(done));
    }

    public static class AdFactory
    {
        public static IAdService Create()
        {
#if CASINO_ADMOB && UNITY_ANDROID && !UNITY_EDITOR
            return new AdMobAds();
#else
            if (GameConfig.TestServices) return new SimulatedAds();
            return new NoAds();
#endif
        }
    }

#if CASINO_ADMOB && UNITY_ANDROID
    /// <summary>
    /// Google AdMob rewarded ads (Google Mobile Ads Unity plugin 11.x). Enable with the scripting define CASINO_ADMOB and
    /// set your AdMob app id in the plugin settings. Shows Google's UMP consent form where the law requires it (EEA/UK).
    /// Plugin callbacks can arrive on a background thread, so every one is marshalled to the main thread.
    /// </summary>
    public sealed class AdMobAds : IAdService
    {
        RewardedAd rewarded;
        bool initialized, loading;
        float retryDelay = 7.5f;
        int failures;

        public bool IsSupported => true;
        public bool IsReady => rewarded != null && rewarded.CanShowAd();
        public bool IsBlocked => failures >= 2 && !IsReady;

        static void OnMain(Action action) => GoogleMobileAds.Common.MobileAdsEventExecutor.ExecuteInUpdate(action);

        public void Initialize()
        {
            GoogleMobileAds.Common.MobileAdsEventExecutor.Initialize();
            ConsentInformation.Update(new ConsentRequestParameters(), updateError => OnMain(() =>
            {
                if (updateError != null) Debug.LogWarning("Consent update failed: " + updateError.Message);
                ConsentForm.LoadAndShowConsentFormIfRequired(formError => OnMain(() =>
                {
                    if (formError != null) Debug.LogWarning("Consent form failed: " + formError.Message);
                    // Test builds only ever load Google's test ads, so they go ahead even when the consent service
                    // can't be reached (e.g. the AdMob test app id has no consent form configured).
                    if (!ConsentInformation.CanRequestAds() && !GameConfig.DemoMode) return;
                    MobileAds.Initialize(_ => OnMain(() =>
                    {
                        initialized = true;
                        Load();
                    }));
                }));
            }));
        }

        void Load()
        {
            if (!initialized || loading) return;
            loading = true;
            RewardedAd.Load(GameConfig.AdMobRewardedAndroid, new AdRequest(), (ad, error) => OnMain(() =>
            {
                loading = false;
                if (error != null || ad == null)
                {
                    Debug.LogWarning("Rewarded ad failed to load: " + error);
                    failures++;
                    retryDelay = Mathf.Min(retryDelay * 2f, 300f);
                    App.I.StartCoroutine(RetryLoad(retryDelay));
                    return;
                }
                retryDelay = 7.5f;
                failures = 0;
                rewarded = ad;
            }));
        }

        System.Collections.IEnumerator RetryLoad(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (rewarded == null) Load();
        }

        public void ShowRewarded(Action<bool> done)
        {
            if (!IsReady)
            {
                done(false);
                Load();
                return;
            }
            bool earned = false, finished = false;
            var ad = rewarded;
            rewarded = null;
            void Finish(bool result)
            {
                if (finished) return;
                finished = true;
                done(result);
                ad.Destroy();
                Load();
            }
            ad.OnAdFullScreenContentClosed += () => OnMain(() => Finish(earned));
            ad.OnAdFullScreenContentFailed += _ => OnMain(() => Finish(false));
            ad.Show(_ => OnMain(() => earned = true));
        }
    }
#endif
}
