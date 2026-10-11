using System;
using System.Collections.Generic;
using System.Globalization;

namespace Casino.Core.Economy
{
    public enum PricingMode
    {
        /// <summary>One SKU per product; per-country prices live in Play Console / Steamworks. Recommended.</summary>
        StoreNative,

        /// <summary>Five SKUs per product (suffix _t1.._t5); the device's verified location picks the tier.</summary>
        LocationTiers,
    }

    /// <summary>Purchasing-power based prices: poorer countries pay proportionally less.</summary>
    public static class RegionalPricing
    {
        public const int TierCount = 5;
        static readonly float[] TierMultipliers = { 1.00f, 0.80f, 0.60f, 0.45f, 0.30f };
        static readonly float[] TierThresholds = { 0.85f, 0.70f, 0.55f, 0.38f };
        static Dictionary<string, float> table;

        public static string DataSource => RegionalPriceTable.DataSource;

        static Dictionary<string, float> Table
        {
            get
            {
                if (table != null) return table;
                var t = new Dictionary<string, float>(256, StringComparer.OrdinalIgnoreCase);
                foreach (var entry in RegionalPriceTable.Data.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    int colon = entry.IndexOf(':');
                    if (colon != 2) continue;
                    t[entry.Substring(0, 2)] = float.Parse(entry.Substring(3), CultureInfo.InvariantCulture);
                }
                return table = t;
            }
        }

        public static IEnumerable<KeyValuePair<string, float>> AllCountries => Table;

        public static bool IsKnownCountry(string iso2) => !string.IsNullOrEmpty(iso2) && Table.ContainsKey(iso2);

        /// <summary>Continuous multiplier for a country; unknown countries pay the base price.</summary>
        public static float Multiplier(string iso2) =>
            !string.IsNullOrEmpty(iso2) && Table.TryGetValue(iso2, out var m) ? m : 1f;

        public static int Tier(string iso2)
        {
            float m = Multiplier(iso2);
            for (int i = 0; i < TierThresholds.Length; i++)
                if (m >= TierThresholds[i]) return i + 1;
            return TierCount;
        }

        public static float TierMultiplier(int tier) => TierMultipliers[Math.Max(1, Math.Min(TierCount, tier)) - 1];

        public static float EffectiveMultiplier(string iso2, PricingMode mode) =>
            mode == PricingMode.LocationTiers ? TierMultiplier(Tier(iso2)) : Multiplier(iso2);

        /// <summary>Regional USD price rounded down to a .x9 price point (minimum 0.19).</summary>
        public static decimal PriceUsd(decimal baseUsd, float multiplier)
        {
            long cents = (long)decimal.Round(baseUsd * (decimal)multiplier * 100m);
            long charm = (cents + 1) / 10 * 10 - 1;
            if (charm < 19) charm = 19;
            if (charm > (long)(baseUsd * 100m)) charm = (long)(baseUsd * 100m);
            return charm / 100m;
        }

        public static decimal PriceUsd(ProductDef product, string iso2, PricingMode mode) =>
            PriceUsd(product.BaseUsd, EffectiveMultiplier(iso2, mode));

        public static string StoreSku(ProductDef product, string iso2, PricingMode mode) =>
            mode == PricingMode.LocationTiers ? product.Id + "_t" + Tier(iso2) : product.Id;

        /// <summary>Maps any SKU (tiered or not) back to its catalog product id.</summary>
        public static string ProductIdFromSku(string sku)
        {
            if (string.IsNullOrEmpty(sku)) return sku;
            int n = sku.Length;
            if (n > 3 && sku[n - 3] == '_' && sku[n - 2] == 't' && sku[n - 1] >= '1' && sku[n - 1] <= '9')
                return sku.Substring(0, n - 3);
            return sku;
        }

        public static string FormatUsd(decimal amount) => "$" + amount.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
