using System;
using System.Collections.Generic;
using UnityEngine;

namespace Casino.Platform
{
    public enum PurchaseResult { Success, Cancelled, Failed, Unavailable }

    /// <summary>
    /// A real-money store. <c>fulfill(sku, transactionId)</c> is called for every completed purchase - including ones
    /// replayed after a crash - and must return true once the goods are granted and saved.
    /// </summary>
    public interface IStoreService
    {
        string Name { get; }
        bool IsAvailable { get; }
        bool IsReady { get; }
        void Initialize(IReadOnlyList<string> skus, Func<string, string, bool> fulfill);
        /// <summary>Price string exactly as the store will charge it (local currency), or null if unknown.</summary>
        string LocalizedPrice(string sku);
        void Purchase(string sku, Action<PurchaseResult> done);
    }

    /// <summary>Release builds on platforms without a store: the shop explains that purchases are not available.</summary>
    public sealed class UnavailableStore : IStoreService
    {
        public string Name => "None";
        public bool IsAvailable => false;
        public bool IsReady => false;
        public void Initialize(IReadOnlyList<string> skus, Func<string, string, bool> fulfill) { }
        public string LocalizedPrice(string sku) => null;
        public void Purchase(string sku, Action<PurchaseResult> done) => done(PurchaseResult.Unavailable);
    }

    /// <summary>Editor and development builds only: a confirmation dialog stands in for the payment sheet.</summary>
    public sealed class SimulatedStore : IStoreService
    {
        Func<string, string, bool> fulfill;

        public string Name => "Simulated (no real money)";

        public bool IsAvailable => true;
        public bool IsReady => fulfill != null;

        public void Initialize(IReadOnlyList<string> skus, Func<string, string, bool> fulfillCallback) => fulfill = fulfillCallback;

        public string LocalizedPrice(string sku) => null;

        public void Purchase(string sku, Action<PurchaseResult> done)
        {
            App.I.UI.Modal<Casino.Screens.SimulatedPurchaseModal>(m => m.Setup(sku, ok =>
            {
                if (!ok)
                {
                    done(PurchaseResult.Cancelled);
                    return;
                }
                bool granted = fulfill(sku, "sim-" + Guid.NewGuid().ToString("N"));
                done(granted ? PurchaseResult.Success : PurchaseResult.Failed);
            }));
        }
    }

    public static class StoreFactory
    {
        public static IStoreService Create()
        {
#if CASINO_STEAMWORKS && UNITY_STANDALONE && !UNITY_EDITOR
            if (SteamBootstrap.Running) return new SteamStore();
#endif
#if CASINO_UNITY_IAP && UNITY_ANDROID && !UNITY_EDITOR
            return new UnityIapStore();
#else
            if (GameConfig.TestServices) return new SimulatedStore();
            return new UnavailableStore();
#endif
        }
    }
}
