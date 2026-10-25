using UnityEngine;
#if CASINO_STEAMWORKS
using System;
using System.Collections.Generic;
using Steamworks;
#endif

namespace Casino.Platform
{
    /// <summary>
    /// Steamworks.NET lifecycle (Windows/Linux Steam builds). Enable with the scripting define CASINO_STEAMWORKS after
    /// adding the Steamworks.NET package. Without the define this compiles to a no-op.
    /// </summary>
    public sealed class SteamBootstrap : MonoBehaviour
    {
        public static bool Running { get; private set; }

        public static void Create(Transform parent)
        {
#if CASINO_STEAMWORKS && UNITY_STANDALONE
            var go = new GameObject("Steam");
            go.transform.SetParent(parent, false);
            go.AddComponent<SteamBootstrap>();
#endif
        }

        public static string IpCountry()
        {
#if CASINO_STEAMWORKS && UNITY_STANDALONE
            if (Running) return SteamUtils.GetIPCountry();
#endif
            return null;
        }

        /// <summary>The language Steam runs the game in, as a language tag ("bg", "pt-BR"), or null outside Steam.</summary>
        public static string Language()
        {
#if CASINO_STEAMWORKS && UNITY_STANDALONE
            if (!Running) return null;
            string api = SteamApps.GetCurrentGameLanguage();
            switch (api)
            {
                case "schinese": return "zh-Hans";
                case "tchinese": return "zh-Hant";
                case "brazilian": return "pt-BR";
                case "portuguese": return "pt-PT";
                case "latam": case "spanish": return "es";
                case "koreana": return "ko";
            }
            foreach (var c in System.Globalization.CultureInfo.GetCultures(System.Globalization.CultureTypes.NeutralCultures))
                if (string.Equals(c.EnglishName, api, StringComparison.OrdinalIgnoreCase)) return c.Name;
#endif
            return null;
        }

#if CASINO_STEAMWORKS && UNITY_STANDALONE
        void Awake()
        {
            try
            {
#if !UNITY_EDITOR
                if (SteamAPI.RestartAppIfNecessary(new AppId_t(GameConfig.SteamAppId)))
                {
                    Application.Quit();
                    return;
                }
#endif
                Running = SteamAPI.Init();
                if (!Running) Debug.LogWarning("SteamAPI.Init failed - is Steam running?");
            }
            catch (DllNotFoundException e)
            {
                Debug.LogError("Steamworks native library missing: " + e.Message);
            }
        }

        void Update()
        {
            if (Running) SteamAPI.RunCallbacks();
        }

        void OnApplicationQuit() => Shutdown();
        void OnDestroy() => Shutdown();

        static void Shutdown()
        {
            if (!Running) return;
            Running = false;
            SteamAPI.Shutdown();
        }
#endif
    }

#if CASINO_STEAMWORKS && UNITY_STANDALONE
    /// <summary>
    /// Steam purchases through the Steam Inventory Service: each product is an item definition with a price category,
    /// which Steam localises per region. Bought items land in the player's inventory and are consumed here to grant them.
    /// </summary>
    public sealed class SteamStore : IStoreService
    {
        Func<string, string, bool> fulfill;
        Action<PurchaseResult> pending;
        CallResult<SteamInventoryStartPurchaseResult_t> startPurchase;
        CallResult<SteamInventoryRequestPricesResult_t> pricesResult;
        Callback<SteamInventoryResultReady_t> resultReady;
        Callback<GameOverlayActivated_t> overlay;
        SteamInventoryResult_t allItems = SteamInventoryResult_t.Invalid;
        readonly Dictionary<string, string> prices = new Dictionary<string, string>();
        readonly HashSet<SteamInventoryResult_t> consuming = new HashSet<SteamInventoryResult_t>();
        bool ready;

        public string Name => "Steam";
        public bool IsAvailable => SteamBootstrap.Running;
        public bool IsReady => ready;

        public void Initialize(IReadOnlyList<string> skus, Func<string, string, bool> fulfillCallback)
        {
            fulfill = fulfillCallback;
            startPurchase = CallResult<SteamInventoryStartPurchaseResult_t>.Create(OnStartPurchase);
            pricesResult = CallResult<SteamInventoryRequestPricesResult_t>.Create(OnPrices);
            resultReady = Callback<SteamInventoryResultReady_t>.Create(OnResultReady);
            overlay = Callback<GameOverlayActivated_t>.Create(OnOverlay);
            pricesResult.Set(SteamInventory.RequestPrices());
            RefreshInventory(); // also grants anything bought while the game was closed
            ready = true;
        }

        public string LocalizedPrice(string sku) => prices.TryGetValue(sku, out var p) ? p : null;

        public void Purchase(string sku, Action<PurchaseResult> done)
        {
            int def = GameConfig.SteamItemDef(Core.Economy.RegionalPricing.ProductIdFromSku(sku));
            if (def == 0)
            {
                done(PurchaseResult.Unavailable);
                return;
            }
            pending = done;
            startPurchase.Set(SteamInventory.StartPurchase(new[] { new SteamItemDef_t(def) }, new uint[] { 1 }, 1));
        }

        void OnStartPurchase(SteamInventoryStartPurchaseResult_t r, bool ioFailure)
        {
            if (ioFailure || r.m_result != EResult.k_EResultOK)
            {
                pending?.Invoke(PurchaseResult.Failed);
                pending = null;
            }
            // Otherwise the Steam overlay checkout is open; the items arrive when it closes.
        }

        void OnOverlay(GameOverlayActivated_t e)
        {
            if (e.m_bActive == 0 && pending != null) RefreshInventory();
        }

        void OnPrices(SteamInventoryRequestPricesResult_t r, bool ioFailure)
        {
            if (ioFailure || r.m_result != EResult.k_EResultOK) return;
            string currency = r.m_rgchCurrency;
            foreach (var product in Core.Economy.ProductCatalog.All)
            {
                int def = GameConfig.SteamItemDef(product.Id);
                if (def != 0 && SteamInventory.GetItemPrice(new SteamItemDef_t(def), out ulong price, out _))
                    prices[product.Id] = (price / 100.0).ToString("0.00") + " " + currency;
            }
        }

        void RefreshInventory()
        {
            if (allItems != SteamInventoryResult_t.Invalid) return;
            SteamInventory.GetAllItems(out allItems);
        }

        void OnResultReady(SteamInventoryResultReady_t e)
        {
            if (consuming.Remove(e.m_handle))
            {
                SteamInventory.DestroyResult(e.m_handle);
                return;
            }
            if (e.m_handle != allItems) return;
            allItems = SteamInventoryResult_t.Invalid;
            bool grantedAny = false;
            if (e.m_result == EResult.k_EResultOK)
            {
                uint count = 0;
                SteamInventory.GetResultItems(e.m_handle, null, ref count);
                var items = new SteamItemDetails_t[count];
                if (count > 0 && SteamInventory.GetResultItems(e.m_handle, items, ref count))
                {
                    foreach (var item in items)
                    {
                        string productId = GameConfig.ProductForSteamItemDef(item.m_iDefinition.m_SteamItemDef);
                        if (productId == null) continue;
                        for (int q = 0; q < item.m_unQuantity; q++)
                        {
                            // The instance id makes the grant idempotent, so a consume that fails and is retried never pays twice.
                            if (!SteamInventory.ConsumeItem(out var consumeHandle, item.m_itemId, 1)) continue;
                            consuming.Add(consumeHandle);
                            grantedAny |= fulfill(productId, "steam-" + item.m_itemId.m_SteamItemInstanceID + "-" + q);
                        }
                    }
                }
            }
            SteamInventory.DestroyResult(e.m_handle);
            if (pending != null)
            {
                pending(grantedAny ? PurchaseResult.Success : PurchaseResult.Cancelled);
                pending = null;
            }
        }
    }
#endif
}
