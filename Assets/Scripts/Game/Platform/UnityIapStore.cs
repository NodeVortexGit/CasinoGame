#if CASINO_UNITY_IAP
using System;
using System.Collections.Generic;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.Purchasing;

namespace Casino.Platform
{
    /// <summary>
    /// Google Play billing through Unity IAP 5 (com.unity.purchasing). Enable with the scripting define CASINO_UNITY_IAP
    /// (menu: Casino > Integrations). Per-country prices are set on each product in Play Console; Google charges by the
    /// account's Play country, which is tied to the payment method, so a VPN does not change the price.
    /// </summary>
    public sealed class UnityIapStore : IStoreService
    {
        StoreController store;
        bool ready;
        Func<string, string, bool> fulfill;
        Action<PurchaseResult> pending;

        public string Name => "Google Play";
        public bool IsAvailable => true;
        public bool IsReady => ready;

        public async void Initialize(IReadOnlyList<string> skus, Func<string, string, bool> fulfillCallback)
        {
            fulfill = fulfillCallback;
            try
            {
                await UnityServices.InitializeAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning("Unity Services init failed (IAP will still try): " + e.Message);
            }

            store = UnityIAPServices.StoreController();
            store.OnPurchasePending += OnPurchasePending;
            store.OnPurchaseFailed += OnPurchaseFailed;
            store.OnProductsFetched += _ =>
            {
                ready = true;
                // Replays purchases that were paid for but never confirmed (crash, killed app) through OnPurchasePending.
                store.FetchPurchases();
            };
            store.OnProductsFetchFailed += f => Debug.LogWarning("IAP product fetch failed: " + f.FailureReason);
            store.OnStoreDisconnected += d => Debug.LogWarning("IAP store disconnected: " + d.message);

            try
            {
                await store.Connect();
            }
            catch (Exception e)
            {
                Debug.LogWarning("IAP connect failed: " + e.Message);
                return;
            }
            var definitions = new List<ProductDefinition>();
            foreach (var sku in skus) definitions.Add(new ProductDefinition(sku, ProductType.Consumable));
            store.FetchProducts(definitions);
        }

        public string LocalizedPrice(string sku)
        {
            var product = store?.GetProductById(sku);
            return product != null && product.availableToPurchase ? product.metadata.localizedPriceString : null;
        }

        public void Purchase(string sku, Action<PurchaseResult> done)
        {
            var product = store?.GetProductById(sku);
            if (!ready || product == null || !product.availableToPurchase)
            {
                done(PurchaseResult.Unavailable);
                return;
            }
            pending = done;
            store.PurchaseProduct(product);
        }

        void OnPurchasePending(PendingOrder order)
        {
            // TODO before launch: verify order.Info.Receipt on your server before granting.
            bool granted = true;
            foreach (var item in order.CartOrdered.Items())
                granted &= fulfill(item.Product.definition.id, order.Info.TransactionID + ":" + item.Product.definition.id);
            if (granted) store.ConfirmPurchase(order);
            pending?.Invoke(granted ? PurchaseResult.Success : PurchaseResult.Failed);
            pending = null;
        }

        void OnPurchaseFailed(FailedOrder order)
        {
            pending?.Invoke(order.FailureReason == PurchaseFailureReason.UserCancelled ? PurchaseResult.Cancelled : PurchaseResult.Failed);
            pending = null;
        }
    }
}
#endif
